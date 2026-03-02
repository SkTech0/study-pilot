using Microsoft.AspNetCore.Mvc;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("version")]
public sealed class VersionController : ControllerBase
{
    private readonly IConfiguration _config;

    public VersionController(IConfiguration config) => _config = config;

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            serviceName = _config["Version:ServiceName"] ?? "StudyPilot.API",
            version = _config["Version:Number"] ?? "1.0.0",
            buildTimestamp = _config["Version:BuildTimestamp"] ?? "",
            environment = _config["ASPNETCORE_ENVIRONMENT"] ?? "Production"
        });
    }
}
