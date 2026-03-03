using MediatR;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Persistence;
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
    private readonly IConceptRepository _conceptRepository;

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
        IConceptRepository conceptRepository)
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
        _conceptRepository = conceptRepository;
    }

    public async Task<Result<SendChatMessageResult>> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        var session = await _chatSessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<SendChatMessageResult>.Failure(new AppError(ErrorCodes.ChatSessionNotFound, "Chat session not found.", "sessionId", ErrorSeverity.Business));
        if (session.UserId != request.UserId)
            return Result<SendChatMessageResult>.Failure(new AppError(ErrorCodes.ChatSessionAccessDenied, "You do not have access to this chat session.", "sessionId", ErrorSeverity.Business));

        var userMessage = new ChatMessage(session.Id, ChatRole.User, request.Content.Trim());
        await _chatMessageRepository.AddAsync(userMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var queryText = request.Content.Trim();
        var queryEmbedding = await _embeddingCache.GetAsync(queryText, cancellationToken);
        if (queryEmbedding is null)
        {
            queryEmbedding = await _embeddingService.EmbedAsync(queryText, cancellationToken);
            await _embeddingCache.SetAsync(queryText, queryEmbedding, cancellationToken);
        }

        var retrieved = await _hybridSearch.SearchAsync(request.UserId, queryEmbedding, session.DocumentId, queryText, TopK, cancellationToken);

        string answerText;
        IReadOnlyList<Guid> cited;

        if (!HasSufficientContext(retrieved))
        {
            answerText = RetrievalConstants.InsufficientContextMessage;
            cited = Array.Empty<Guid>();
        }
        else
        {
            var explanationStyle = await ResolveExplanationStyleAsync(request.UserId, session.DocumentId, cancellationToken);
            var chatRequest = new ChatRequest(
                request.UserId,
                session.Id,
                session.DocumentId,
                queryText,
                retrieved,
                GroundingSystemInstruction,
                explanationStyle);
            var answer = await _chatService.GenerateAnswerAsync(chatRequest, cancellationToken);
            answerText = answer.Answer;
            var allowedChunkIds = new HashSet<Guid>(retrieved.Select(c => c.ChunkId));
            cited = (answer.CitedChunkIds ?? Array.Empty<Guid>()).Where(allowedChunkIds.Contains).Distinct().ToList();
        }

        var assistantMessage = new ChatMessage(session.Id, ChatRole.Assistant, answerText);
        await _chatMessageRepository.AddAsync(assistantMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (cited.Count > 0)
        {
            await _citationRepository.AddRangeAsync(assistantMessage.Id, cited, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<SendChatMessageResult>.Success(new SendChatMessageResult(assistantMessage.Id, answerText, cited.ToList()));
    }

    private async Task<ExplanationStyle?> ResolveExplanationStyleAsync(Guid userId, Guid? documentId, CancellationToken cancellationToken)
    {
        if (!documentId.HasValue) return null;
        var concepts = await _conceptRepository.GetByDocumentIdAsync(documentId.Value, cancellationToken);
        if (concepts.Count == 0) return null;
        var conceptIds = concepts.Select(c => c.Id).ToList();
        var masteries = await _masteryRepository.GetByUserAndConceptsAsync(userId, conceptIds, cancellationToken);
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

