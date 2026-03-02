using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace StudyPilot.API.Services;

public static class StartupConfigurationValidator
{
    public static void Validate(IConfiguration config)
    {
        var missing = new List<string>();
        var jwt = config.GetSection("Jwt");
        var jwtSecret = jwt["Key"] ?? jwt["Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            missing.Add("Jwt__Key or Jwt:Secret");
        else if (System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 16)
            missing.Add("Jwt secret must be at least 16 bytes (128 bits) for HS256");
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("Default")) && string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
            missing.Add("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(config["AIService:BaseUrl"]))
            missing.Add("AIService__BaseUrl");
        if (string.IsNullOrWhiteSpace(config["AIService:TimeoutSeconds"]))
            missing.Add("AIService__TimeoutSeconds");
        if (missing.Count == 0) return;
        var message = $"Missing required configuration: {string.Join(", ", missing)}";
        Serilog.Log.Fatal(message);
        throw new HostAbortedException(message);
    }
}
