using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetLearningOverview;

public sealed record GetLearningOverviewQuery(Guid UserId) : IRequest<Result<LearningOverviewResult>>;

public sealed record LearningOverviewResult(
    int TotalConcepts,
    double AverageMastery,
    int WeakCount,
    int MediumCount,
    int StrongCount,
    IReadOnlyList<MasteryDistributionItem> Distribution);

public sealed record MasteryDistributionItem(string Bucket, int Count);
