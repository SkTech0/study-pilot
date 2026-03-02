using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public sealed class ChatSession : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid? DocumentId { get; private set; }

    private readonly List<ChatMessage> _messages = new();
    public IReadOnlyCollection<ChatMessage> Messages => _messages.AsReadOnly();

    public ChatSession(Guid userId, Guid? documentId = null) : base()
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        UserId = userId;
        DocumentId = documentId;
    }

    public ChatSession(Guid id, Guid userId, Guid? documentId, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        DocumentId = documentId;
    }

    public void AddMessage(ChatMessage message)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (message.SessionId != Id) throw new InvalidOperationException("Message must belong to this session.");
        _messages.Add(message);
        Touch();
    }
}

