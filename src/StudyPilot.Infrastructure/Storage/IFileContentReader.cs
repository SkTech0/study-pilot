namespace StudyPilot.Infrastructure.Storage;

public interface IFileContentReader
{
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
}
