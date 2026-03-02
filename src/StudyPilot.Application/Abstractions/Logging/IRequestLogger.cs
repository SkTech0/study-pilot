namespace StudyPilot.Application.Abstractions.Logging;

public interface IRequestLogger
{
    void LogInformation(string messageTemplate, params object[] propertyValues);
}
