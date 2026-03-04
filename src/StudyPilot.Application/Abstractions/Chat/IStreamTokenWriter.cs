namespace StudyPilot.Application.Abstractions.Chat;

/// <summary>
/// Abstraction for writing streamed tokens to the client. Allows background worker to write without holding request-scoped resources.
/// </summary>
public interface IStreamTokenWriter
{
    Task WriteAsync(string token, CancellationToken cancellationToken = default);
    void Complete();
}
