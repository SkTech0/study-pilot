using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using StudyPilot.Infrastructure.Storage;

namespace StudyPilot.API.Extensions;

internal sealed class ConfigureStorageOptions : IPostConfigureOptions<StorageOptions>
{
    private readonly IWebHostEnvironment _env;

    public ConfigureStorageOptions(IWebHostEnvironment env) => _env = env;

    public void PostConfigure(string? name, StorageOptions options)
    {
        if (string.IsNullOrEmpty(options.UploadsBasePath))
            options.UploadsBasePath = Path.Combine(_env.ContentRootPath, "uploads");
    }
}
