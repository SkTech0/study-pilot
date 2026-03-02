namespace StudyPilot.API.Extensions;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _env;
    private const string BaseFolder = "uploads";

    public LocalFileStorage(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveAsync(Stream content, string fileName, Guid userId, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName) ?? "document.pdf";
        if (string.IsNullOrEmpty(safeName) || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            safeName = "document.pdf";
        var baseDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, BaseFolder));
        var dir = Path.GetFullPath(Path.Combine(baseDir, userId.ToString()));
        if (!dir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Storage path would escape base directory.");
        Directory.CreateDirectory(dir);
        var path = Path.GetFullPath(Path.Combine(dir, $"{Guid.NewGuid()}_{safeName}"));
        if (!path.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Storage path would escape base directory.");
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, cancellationToken);
        return path;
    }
}
