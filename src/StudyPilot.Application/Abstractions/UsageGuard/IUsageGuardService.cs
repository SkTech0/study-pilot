namespace StudyPilot.Application.Abstractions.UsageGuard;

public interface IUsageGuardService
{
    Task<bool> CanUploadDocumentAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanGenerateQuizAsync(Guid userId, CancellationToken cancellationToken = default);
}
