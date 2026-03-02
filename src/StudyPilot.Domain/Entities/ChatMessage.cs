using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

public sealed class ChatMessage : BaseEntity
{
    public Guid SessionId { get; private set; }
    public ChatRole Role { get; private set; }
    public string Content { get; private set; }

    public ChatMessage(Guid sessionId, ChatRole role, string content) : base()
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        Content = string.IsNullOrWhiteSpace(content) ? throw new ArgumentException("Content cannot be empty.", nameof(content)) : content;
        SessionId = sessionId;
        Role = role;
    }

    public ChatMessage(Guid id, Guid sessionId, ChatRole role, string content, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        SessionId = sessionId;
        Role = role;
        Content = content;
    }
}

