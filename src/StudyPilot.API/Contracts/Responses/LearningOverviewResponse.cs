namespace StudyPilot.API.Contracts.Responses;

public sealed record LearningOverviewResponse(
    int TotalConcepts,
    double AverageMastery,
    int WeakCount,
    int MediumCount,
    int StrongCount,
    IReadOnlyList<MasteryDistributionItemResponse> Distribution);

public sealed record MasteryDistributionItemResponse(string Bucket, int Count);
