using Microsoft.Extensions.Options;

namespace StudyPilot.Infrastructure.Storage;

public sealed class LocalFileContentReader : IFileContentReader
{
    private readonly StorageOptions _options;

    public LocalFileContentReader(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_options.UploadsBasePath))
        {
            var fullPath = Path.GetFullPath(path);
            var basePath = Path.GetFullPath(_options.UploadsBasePath);
            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Path is not under allowed storage base.");
        }
        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
