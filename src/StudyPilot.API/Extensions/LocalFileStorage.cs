namespace StudyPilot.API.Extensions;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _env;
    private const string BaseFolder = "uploads";

    public LocalFileStorage(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveAsync(Stream content, string fileName, Guid userId, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        var dir = Path.Combine(_env.ContentRootPath, BaseFolder, userId.ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid()}_{safeName}");
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, cancellationToken);
        return path;
    }
}
