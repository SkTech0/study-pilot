namespace StudyPilot.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string? UploadsBasePath { get; set; }
}
