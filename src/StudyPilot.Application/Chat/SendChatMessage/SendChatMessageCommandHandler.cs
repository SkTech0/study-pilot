using MediatR;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Chat;
using StudyPilot.Application.Chat.Constants;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Knowledge;
using StudyPilot.Application.Knowledge.Constants;
using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Chat.SendChatMessage;

public sealed class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, Result<SendChatMessageResult>>
{
    private const int TopK = 12;
    private const string GroundingSystemInstruction =
        "Answer ONLY using the provided context. If the answer is not in the context, say you don't know. " +
        "When you use context, cite the chunk ids you used.";

    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatMessageCitationRepository _citationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQueryEmbeddingCache _embeddingCache;
    private readonly IHybridSearchService _hybridSearch;
    private readonly IChatService _chatService;
    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;
    private readonly ILogger<SendChatMessageCommandHandler>? _logger;

    public SendChatMessageCommandHandler(
        IChatSessionRepository chatSessionRepository,
        IChatMessageRepository chatMessageRepository,
        IChatMessageCitationRepository citationRepository,
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IQueryEmbeddingCache embeddingCache,
        IHybridSearchService hybridSearch,
        IChatService chatService,
        IUserConceptMasteryRepository masteryRepository,
        ICorrelationIdAccessor? correlationIdAccessor = null,
        ILogger<SendChatMessageCommandHandler>? logger = null)
    {
        _chatSessionRepository = chatSessionRepository;
        _chatMessageRepository = chatMessageRepository;
        _citationRepository = citationRepository;
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _embeddingCache = embeddingCache;
        _hybridSearch = hybridSearch;
        _chatService = chatService;
        _masteryRepository = masteryRepository;
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task<Result<SendChatMessageResult>> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        var content = (request.Content ?? "").Trim();
        if (content.Length > ChatConstants.MaxMessageLength)
            return Result<SendChatMessageResult>.Failure(new AppError(ErrorCodes.ValidationFailed, "Message exceeds maximum length.", "content", ErrorSeverity.Validation, null, FailureCategory.ValidationFailure));

        var session = await _chatSessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<SendChatMessageResult>.Failure(new AppError(ErrorCodes.ChatSessionNotFound, "Chat session not found.", "sessionId", ErrorSeverity.Business));
        if (session.UserId != request.UserId)
            return Result<SendChatMessageResult>.Failure(new AppError(ErrorCodes.ChatSessionAccessDenied, "You do not have access to this chat session.", "sessionId", ErrorSeverity.Business));

        var userMessage = new ChatMessage(session.Id, ChatRole.User, content);
        await _chatMessageRepository.AddAsync(userMessage, cancellationToken);

        var queryText = content;
        var masteryTask = ResolveExplanationStyleAsync(request.UserId, session.DocumentId, cancellationToken);
        var queryEmbedding = await _embeddingCache.GetAsync(queryText, cancellationToken);
        if (queryEmbedding is null)
        {
            queryEmbedding = await _embeddingService.EmbedAsync(queryText, cancellationToken);
            await _embeddingCache.SetAsync(queryText, queryEmbedding, cancellationToken);
        }
        var explanationStyle = await masteryTask;

        var retrieved = await _hybridSearch.SearchAsync(request.UserId, queryEmbedding, session.DocumentId, queryText, TopK, cancellationToken);

        var chunkCount = retrieved.Count;
        var bestDistance = retrieved.Count > 0 ? retrieved.Min(c => c.Score) : 1.0;
        var bestSimilarity = 1.0 - bestDistance;
        var documentIds = retrieved.Select(c => c.DocumentId).Distinct().ToList();
        _logger?.LogInformation(
            "Retrieval decision request_id={RequestId} session_id={SessionId} chunk_count={ChunkCount} best_similarity={BestSimilarity} document_ids={DocumentIds} sufficient={Sufficient}",
            _correlationIdAccessor?.Get(),
            request.SessionId,
            chunkCount,
            bestSimilarity,
            string.Join(",", documentIds.Select(id => id.ToString())),
            HasSufficientContext(retrieved));

        string answerText;
        IReadOnlyList<Guid> cited;
        var status = ChatStatus.Ok;
        string? reason = null;

        if (!HasSufficientContext(retrieved))
        {
            reason = chunkCount < RetrievalConstants.MinimumChunksForAnswer
                ? $"chunk_count {chunkCount} < minimum {RetrievalConstants.MinimumChunksForAnswer}"
                : $"best_similarity {bestSimilarity:F4} < threshold {RetrievalConstants.MinimumSimilarityThreshold}";
            answerText = retrieved.Count == 0
                ? RetrievalConstants.InsufficientContextMessage + RetrievalConstants.EmbeddingsDelayedRetryHint
                : RetrievalConstants.InsufficientContextMessage;
            cited = Array.Empty<Guid>();
            status = ChatStatus.InsufficientContext;
        }
        else
        {
            var chatRequest = new ChatRequest(
                request.UserId,
                session.Id,
                session.DocumentId,
                queryText,
                retrieved,
                GroundingSystemInstruction,
                explanationStyle);
            try
            {
                var answer = await _chatService.GenerateAnswerAsync(chatRequest, cancellationToken);
                answerText = (answer.Answer ?? "").Trim();
                if (string.IsNullOrWhiteSpace(answerText))
                {
                    answerText = "I'm temporarily unable to get a response from the AI. Please try again shortly.";
                    status = ChatStatus.Fallback;
                }
                else if (answer.FallbackUsed)
                {
                    status = ChatStatus.Fallback;
                }
                var allowedChunkIds = new HashSet<Guid>(retrieved.Select(c => c.ChunkId));
                cited = (answer.CitedChunkIds ?? Array.Empty<Guid>()).Where(allowedChunkIds.Contains).Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Chat provider failed request_id={RequestId} session_id={SessionId}", _correlationIdAccessor?.Get(), request.SessionId);
                answerText = "I'm temporarily unable to reach the AI provider. Please try again shortly.";
                cited = Array.Empty<Guid>();
                status = ChatStatus.Error;
            }
        }

        if (string.IsNullOrWhiteSpace(answerText))
            answerText = "I'm temporarily unable to reach the AI provider. Please try again shortly.";

        var assistantMessage = new ChatMessage(session.Id, ChatRole.Assistant, answerText);
        await _chatMessageRepository.AddAsync(assistantMessage, cancellationToken);
        if (cited.Count > 0)
            await _citationRepository.AddRangeAsync(assistantMessage.Id, cited, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "Chat request completed request_id={RequestId} session_id={SessionId} chunk_count={ChunkCount} retrieval_score={RetrievalScore} status={Status}",
            _correlationIdAccessor?.Get(),
            request.SessionId,
            chunkCount,
            bestSimilarity,
            status.ToApiString());

        return Result<SendChatMessageResult>.Success(new SendChatMessageResult(
            assistantMessage.Id,
            answerText,
            cited.ToList(),
            status,
            chunkCount,
            bestSimilarity,
            reason));
    }

    private async Task<ExplanationStyle?> ResolveExplanationStyleAsync(Guid userId, Guid? documentId, CancellationToken cancellationToken)
    {
        if (!documentId.HasValue) return null;
        var masteries = await _masteryRepository.GetByUserAndDocumentAsync(userId, documentId.Value, cancellationToken);
        if (masteries.Count == 0) return null;
        var avg = masteries.Average(m => m.MasteryScore);
        return ExplanationStyleResolver.FromAverageMastery(avg);
    }

    private static bool HasSufficientContext(IReadOnlyList<RetrievedChunk> retrieved)
    {
        if (retrieved.Count < RetrievalConstants.MinimumChunksForAnswer)
            return false;
        var bestDistance = retrieved.Min(c => c.Score);
        var similarity = 1.0 - bestDistance;
        return similarity >= RetrievalConstants.MinimumSimilarityThreshold;
    }
}

