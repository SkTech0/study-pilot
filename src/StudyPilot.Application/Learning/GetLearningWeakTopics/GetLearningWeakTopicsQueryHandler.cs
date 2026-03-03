using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetLearningWeakTopics;

public sealed class GetLearningWeakTopicsQueryHandler : IRequestHandler<GetLearningWeakTopicsQuery, Result<LearningWeakTopicsResult>>
{
    private const int WeakThreshold = 40;

    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IConceptRepository _conceptRepository;

    public GetLearningWeakTopicsQueryHandler(
        IUserConceptMasteryRepository masteryRepository,
        IConceptRepository conceptRepository)
    {
        _masteryRepository = masteryRepository;
        _conceptRepository = conceptRepository;
    }

    public async Task<Result<LearningWeakTopicsResult>> Handle(GetLearningWeakTopicsQuery request, CancellationToken cancellationToken)
    {
        var list = await _masteryRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var weak = list.Where(m => m.MasteryScore <= WeakThreshold)
            .OrderBy(m => m.MasteryScore)
            .Take(request.MaxCount)
            .ToList();
        if (weak.Count == 0)
            return Result<LearningWeakTopicsResult>.Success(new LearningWeakTopicsResult(Array.Empty<WeakTopicItem>()));

        var conceptIds = weak.Select(m => m.ConceptId).Distinct().ToList();
        var concepts = await _conceptRepository.GetByIdsAsync(conceptIds, cancellationToken);
        var conceptMap = concepts.ToDictionary(c => c.Id);

        var topics = weak
            .Where(m => conceptMap.TryGetValue(m.ConceptId, out _))
            .Select(m => new WeakTopicItem(m.ConceptId, conceptMap[m.ConceptId].Name, m.MasteryScore))
            .ToList();

        return Result<LearningWeakTopicsResult>.Success(new LearningWeakTopicsResult(topics));
    }
}
