using Serilog;
using StudyPilot.Application.Abstractions.Logging;

namespace StudyPilot.Infrastructure.Logging;

public sealed class SerilogRequestLogger : IRequestLogger
{
    public void LogInformation(string messageTemplate, params object[] propertyValues) =>
        Log.Information(messageTemplate, propertyValues);
}
