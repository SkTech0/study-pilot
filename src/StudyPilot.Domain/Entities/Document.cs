using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Domain.Entities;

public class Document : BaseEntity
{
    public Guid UserId { get; private set; }
    public string FileName { get; private set; }
    public string StoragePath { get; private set; }
    public ProcessingStatus ProcessingStatus { get; private set; }

    private readonly List<Concept> _concepts = new();
    public IReadOnlyCollection<Concept> Concepts => _concepts.AsReadOnly();

    public Document(Guid userId, string fileName, string storagePath) : base()
    {
        UserId = userId;
        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("File name cannot be empty.", nameof(fileName))
            : fileName;
        StoragePath = string.IsNullOrWhiteSpace(storagePath)
            ? throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath))
            : storagePath;
        ProcessingStatus = Enums.ProcessingStatus.Pending;
    }

    public Document(Guid id, Guid userId, string fileName, string storagePath, ProcessingStatus processingStatus, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        UserId = userId;
        FileName = fileName;
        StoragePath = storagePath;
        ProcessingStatus = processingStatus;
    }

    public void AddConcept(Concept concept)
    {
        if (concept is null)
            throw new ArgumentNullException(nameof(concept));
        if (concept.DocumentId != Id)
            throw new InvalidOperationException("Concept must belong to this document.");
        _concepts.Add(concept);
        Touch();
    }

    public void SetProcessingStatus(ProcessingStatus status)
    {
        ProcessingStatus = status;
        Touch();
    }
}
