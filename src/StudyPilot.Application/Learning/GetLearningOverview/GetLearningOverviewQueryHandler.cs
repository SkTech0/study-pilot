using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetLearningOverview;

public sealed class GetLearningOverviewQueryHandler : IRequestHandler<GetLearningOverviewQuery, Result<LearningOverviewResult>>
{
    private readonly IUserConceptMasteryRepository _masteryRepository;

    public GetLearningOverviewQueryHandler(IUserConceptMasteryRepository masteryRepository)
    {
        _masteryRepository = masteryRepository;
    }

    public async Task<Result<LearningOverviewResult>> Handle(GetLearningOverviewQuery request, CancellationToken cancellationToken)
    {
        var list = await _masteryRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (list.Count == 0)
            return Result<LearningOverviewResult>.Success(new LearningOverviewResult(0, 0, 0, 0, 0, Array.Empty<MasteryDistributionItem>()));

        var total = list.Count;
        var avg = list.Average(m => m.MasteryScore);
        var weak = list.Count(m => m.MasteryScore <= 40);
        var medium = list.Count(m => m.MasteryScore > 40 && m.MasteryScore <= 70);
        var strong = list.Count(m => m.MasteryScore > 70);

        var distribution = new[]
        {
            new MasteryDistributionItem("Weak", weak),
            new MasteryDistributionItem("Medium", medium),
            new MasteryDistributionItem("Strong", strong)
        };

        return Result<LearningOverviewResult>.Success(new LearningOverviewResult(
            total, avg, weak, medium, strong, distribution));
    }
}
