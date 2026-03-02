namespace StudyPilot.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    protected BaseEntity(Guid id, DateTime createdAtUtc, DateTime updatedAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    protected void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
