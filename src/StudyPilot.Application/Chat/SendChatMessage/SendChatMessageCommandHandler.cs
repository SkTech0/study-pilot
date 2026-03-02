using MediatR;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
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
    private readonly IVectorSearchService _vectorSearch;
    private readonly IChatService _chatService;

    public SendChatMessageCommandHandler(
        IChatSessionRepository chatSessionRepository,
        IChatMessageRepository chatMessageRepository,
        IChatMessageCitationRepository citationRepository,
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearch,
        IChatService chatService)
    {
        _chatSessionRepository = chatSessionRepository;
        _chatMessageRepository = chatMessageRepository;
        _citationRepository = citationRepository;
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _vectorSearch = vectorSearch;
        _chatService = chatService;
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

        var queryEmbedding = await _embeddingService.EmbedAsync(request.Content, cancellationToken);
        var retrieved = await _vectorSearch.SearchAsync(request.UserId, queryEmbedding, session.DocumentId, TopK, cancellationToken);

        var chatRequest = new ChatRequest(
            request.UserId,
            session.Id,
            session.DocumentId,
            request.Content,
            retrieved,
            GroundingSystemInstruction);

        var answer = await _chatService.GenerateAnswerAsync(chatRequest, cancellationToken);
        var assistantMessage = new ChatMessage(session.Id, ChatRole.Assistant, answer.Answer);
        await _chatMessageRepository.AddAsync(assistantMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var allowedChunkIds = new HashSet<Guid>(retrieved.Select(c => c.ChunkId));
        var cited = (answer.CitedChunkIds ?? Array.Empty<Guid>()).Where(allowedChunkIds.Contains).Distinct().ToList();
        if (cited.Count > 0)
        {
            await _citationRepository.AddRangeAsync(assistantMessage.Id, cited, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<SendChatMessageResult>.Success(new SendChatMessageResult(assistantMessage.Id, answer.Answer, cited));
    }
}

