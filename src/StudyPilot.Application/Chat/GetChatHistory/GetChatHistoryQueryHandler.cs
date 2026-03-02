using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Chat.GetChatHistory;

public sealed class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, Result<GetChatHistoryResult>>
{
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatMessageCitationRepository _citationRepository;

    public GetChatHistoryQueryHandler(
        IChatSessionRepository chatSessionRepository,
        IChatMessageRepository chatMessageRepository,
        IChatMessageCitationRepository citationRepository)
    {
        _chatSessionRepository = chatSessionRepository;
        _chatMessageRepository = chatMessageRepository;
        _citationRepository = citationRepository;
    }

    public async Task<Result<GetChatHistoryResult>> Handle(GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        var session = await _chatSessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<GetChatHistoryResult>.Failure(new AppError(ErrorCodes.ChatSessionNotFound, "Chat session not found.", "sessionId", ErrorSeverity.Business));
        if (session.UserId != request.UserId)
            return Result<GetChatHistoryResult>.Failure(new AppError(ErrorCodes.ChatSessionAccessDenied, "You do not have access to this chat session.", "sessionId", ErrorSeverity.Business));

        var take = request.PageSize <= 0 ? 50 : Math.Min(200, request.PageSize);
        var skip = Math.Max(0, (request.PageNumber - 1) * take);

        var total = await _chatMessageRepository.CountBySessionIdAsync(session.Id, cancellationToken);
        var messages = await _chatMessageRepository.GetBySessionIdAsync(session.Id, skip, take, cancellationToken);

        var messageIds = messages.Select(m => m.Id).ToList();
        var citationMap = messageIds.Count == 0
            ? (IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>)new Dictionary<Guid, IReadOnlyList<Guid>>()
            : await _citationRepository.GetChunkIdsByMessageIdsAsync(messageIds, cancellationToken);

        var items = messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m =>
            {
                var cited = citationMap.TryGetValue(m.Id, out var ids) ? ids : Array.Empty<Guid>();
                return new ChatMessageItem(m.Id, m.Role, m.Content, m.CreatedAtUtc, cited);
            })
            .ToList();

        return Result<GetChatHistoryResult>.Success(new GetChatHistoryResult(session.Id, total, request.PageNumber, take, items));
    }
}

