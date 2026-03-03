using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetLearningProgress;

public sealed class GetLearningProgressQueryHandler : IRequestHandler<GetLearningProgressQuery, Result<LearningProgressResult>>
{
    private const int TakeCount = 10;

    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IConceptRepository _conceptRepository;

    public GetLearningProgressQueryHandler(
        IUserConceptMasteryRepository masteryRepository,
        IConceptRepository conceptRepository)
    {
        _masteryRepository = masteryRepository;
        _conceptRepository = conceptRepository;
    }

    public async Task<Result<LearningProgressResult>> Handle(GetLearningProgressQuery request, CancellationToken cancellationToken)
    {
        var list = await _masteryRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (list.Count == 0)
            return Result<LearningProgressResult>.Success(new LearningProgressResult(
                Array.Empty<ConceptProgressItem>(),
                Array.Empty<ConceptProgressItem>(),
                0));

        var conceptIds = list.Select(m => m.ConceptId).Distinct().ToList();
        var concepts = await _conceptRepository.GetByIdsAsync(conceptIds, cancellationToken);
        var conceptMap = concepts.ToDictionary(c => c.Id);

        var strongest = list.OrderByDescending(m => m.MasteryScore).Take(TakeCount)
            .Where(m => conceptMap.TryGetValue(m.ConceptId, out _))
            .Select(m => new ConceptProgressItem(m.ConceptId, conceptMap[m.ConceptId].Name, m.MasteryScore))
            .ToList();
        var weakest = list.OrderBy(m => m.MasteryScore).Take(TakeCount)
            .Where(m => conceptMap.TryGetValue(m.ConceptId, out _))
            .Select(m => new ConceptProgressItem(m.ConceptId, conceptMap[m.ConceptId].Name, m.MasteryScore))
            .ToList();

        var avgMastery = list.Average(m => m.MasteryScore);
        return Result<LearningProgressResult>.Success(new LearningProgressResult(
            strongest, weakest, avgMastery));
    }
}
