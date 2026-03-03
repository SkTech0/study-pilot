using Microsoft.Extensions.Options;
using System.Text;
using UglyToad.PdfPig;

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

        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var document = PdfDocument.Open(path);
                var sb = new StringBuilder(capacity: 16_384);
                foreach (var page in document.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(page.Text))
                        sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }, cancellationToken);
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
