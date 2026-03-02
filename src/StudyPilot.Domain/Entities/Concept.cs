using StudyPilot.Domain.Common;

namespace StudyPilot.Domain.Entities;

public class Concept : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    public Concept(Guid documentId, string name, string? description = null) : base()
    {
        DocumentId = documentId;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Concept name cannot be empty.", nameof(name))
            : name;
        Description = description;
    }

    public Concept(Guid id, Guid documentId, string name, string? description, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        DocumentId = documentId;
        Name = name;
        Description = description;
    }
}
