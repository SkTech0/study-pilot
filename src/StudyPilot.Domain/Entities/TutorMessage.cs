using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public sealed class TutorMessage : BaseEntity
{
    public Guid TutorSessionId { get; private set; }
    public string Role { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public TutorMessage(Guid tutorSessionId, string role, string content) : base()
    {
        TutorSessionId = tutorSessionId;
        Role = role ?? "user";
        Content = content ?? "";
        CreatedUtc = DateTime.UtcNow;
    }

    internal TutorMessage(Guid id, Guid tutorSessionId, string role, string content, DateTime createdUtc,
        DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        TutorSessionId = tutorSessionId;
        Role = role;
        Content = content;
        CreatedUtc = createdUtc;
    }
}
