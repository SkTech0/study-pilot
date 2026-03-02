namespace StudyPilot.API.Extensions;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, Guid userId, CancellationToken cancellationToken = default);
}
