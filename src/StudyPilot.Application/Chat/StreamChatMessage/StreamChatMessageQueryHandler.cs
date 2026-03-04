using System.Threading.Channels;
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

namespace StudyPilot.Application.Chat.StreamChatMessage;

public sealed class StreamChatMessageQueryHandler : IRequestHandler<StreamChatMessageQuery, Result<StreamChatMessageResult>>
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
    private readonly ILogger<StreamChatMessageQueryHandler>? _logger;

    public StreamChatMessageQueryHandler(
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
        ILogger<StreamChatMessageQueryHandler>? logger = null)
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

    public async Task<Result<StreamChatMessageResult>> Handle(StreamChatMessageQuery request, CancellationToken cancellationToken)
    {
        var message = (request.Message ?? "").Trim();
        if (message.Length > ChatConstants.MaxMessageLength)
            return Result<StreamChatMessageResult>.Failure(new AppError(ErrorCodes.ValidationFailed, "Message exceeds maximum length.", "message", ErrorSeverity.Validation, null, FailureCategory.ValidationFailure));

        var session = await _chatSessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
            return Result<StreamChatMessageResult>.Failure(new AppError(ErrorCodes.ChatSessionNotFound, "Chat session not found.", "sessionId", ErrorSeverity.Business));
        if (session.UserId != request.UserId)
            return Result<StreamChatMessageResult>.Failure(new AppError(ErrorCodes.ChatSessionAccessDenied, "You do not have access to this chat session.", "sessionId", ErrorSeverity.Business));

        var userMessage = new ChatMessage(session.Id, ChatRole.User, message);
        await _chatMessageRepository.AddAsync(userMessage, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var queryText = message;
        var queryEmbedding = await _embeddingCache.GetAsync(queryText, cancellationToken).ConfigureAwait(false);
        if (queryEmbedding is null)
        {
            queryEmbedding = await _embeddingService.EmbedAsync(queryText, cancellationToken).ConfigureAwait(false);
            await _embeddingCache.SetAsync(queryText, queryEmbedding, cancellationToken).ConfigureAwait(false);
        }

        var retrieved = await _hybridSearch.SearchAsync(request.UserId, queryEmbedding, session.DocumentId, queryText, TopK, cancellationToken).ConfigureAwait(false);

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

        if (!HasSufficientContext(retrieved))
        {
            var controlledMessage = retrieved.Count == 0
                ? RetrievalConstants.InsufficientContextMessage + RetrievalConstants.EmbeddingsDelayedRetryHint
                : RetrievalConstants.InsufficientContextMessage;
            var guardChannel = Channel.CreateUnbounded<string>();
            guardChannel.Writer.TryWrite(controlledMessage);
            guardChannel.Writer.Complete();
            var assistantMsg = new ChatMessage(session.Id, ChatRole.Assistant, controlledMessage);
            await _chatMessageRepository.AddAsync(assistantMsg, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            var tcs = new TaskCompletionSource();
            tcs.SetResult();
            var statusTcsInsufficient = new TaskCompletionSource<ChatStatus>();
            statusTcsInsufficient.SetResult(ChatStatus.InsufficientContext);
            return Result<StreamChatMessageResult>.Success(new StreamChatMessageResult(
                guardChannel.Reader.ReadAllAsync(cancellationToken),
                tcs.Task,
                statusTcsInsufficient.Task));
        }

        var explanationStyle = await ResolveExplanationStyleAsync(request.UserId, session.DocumentId, cancellationToken).ConfigureAwait(false);
        var chatRequest = new ChatRequest(
            request.UserId,
            session.Id,
            session.DocumentId,
            queryText,
            retrieved,
            GroundingSystemInstruction,
            explanationStyle);

        var channel = Channel.CreateUnbounded<string>();
        var sb = new System.Text.StringBuilder();
        var tcsComplete = new TaskCompletionSource();
        var statusTcs = new TaskCompletionSource<ChatStatus>();

        _ = Task.Run(async () =>
        {
            var status = ChatStatus.Ok;
            try
            {
                var streamResult = await _chatService.StreamChatAsync(chatRequest, async token =>
                {
                    sb.Append(token);
                    await channel.Writer.WriteAsync(token, cancellationToken).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);

                if (streamResult.FallbackUsed)
                    status = ChatStatus.Fallback;

                channel.Writer.Complete();

                var fullText = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(fullText))
                {
                    fullText = "I'm temporarily unable to get a response. Please try again shortly.";
                    status = ChatStatus.Fallback;
                }
                var assistantMessage = new ChatMessage(session.Id, ChatRole.Assistant, fullText);
                await _chatMessageRepository.AddAsync(assistantMessage, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var allowedChunkIds = new HashSet<Guid>(retrieved.Select(c => c.ChunkId));
                var cited = (streamResult.CitedChunkIds ?? Array.Empty<Guid>()).Where(allowedChunkIds.Contains).Distinct().ToList();
                if (cited.Count > 0)
                {
                    await _citationRepository.AddRangeAsync(assistantMessage.Id, cited, cancellationToken).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                statusTcs.TrySetResult(status);
                tcsComplete.SetResult();
            }
            catch (OperationCanceledException)
            {
                channel.Writer.Complete();
                statusTcs.TrySetResult(ChatStatus.Error);
                tcsComplete.SetCanceled();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Stream chat provider failed request_id={RequestId} session_id={SessionId}", _correlationIdAccessor?.Get(), request.SessionId);
                var fallback = "I'm temporarily unable to reach the AI provider. Please try again shortly.";
                try { await channel.Writer.WriteAsync(fallback, cancellationToken).ConfigureAwait(false); } catch { /* ignore */ }
                channel.Writer.Complete();
                var assistantMsg = new ChatMessage(session.Id, ChatRole.Assistant, fallback);
                try
                {
                    await _chatMessageRepository.AddAsync(assistantMsg, cancellationToken).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch { /* best effort persist */ }
                statusTcs.TrySetResult(ChatStatus.Error);
                tcsComplete.TrySetResult();
            }
        }, cancellationToken);

        return Result<StreamChatMessageResult>.Success(new StreamChatMessageResult(
            channel.Reader.ReadAllAsync(cancellationToken),
            tcsComplete.Task,
            statusTcs.Task));
    }

    private async Task<ExplanationStyle?> ResolveExplanationStyleAsync(Guid userId, Guid? documentId, CancellationToken cancellationToken)
    {
        if (!documentId.HasValue) return null;
        var masteries = await _masteryRepository.GetByUserAndDocumentAsync(userId, documentId.Value, cancellationToken).ConfigureAwait(false);
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
