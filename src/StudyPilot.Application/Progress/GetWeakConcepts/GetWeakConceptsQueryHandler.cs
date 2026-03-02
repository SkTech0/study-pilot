using StudyPilot.Application.Abstractions.Caching;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;
using MediatR;

namespace StudyPilot.Application.Progress.GetWeakConcepts;

public sealed class GetWeakConceptsQueryHandler : IRequestHandler<GetWeakConceptsQuery, Result<IReadOnlyList<WeakConceptItem>>>
{
    private const int WeakThreshold = 40;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IUserConceptProgressRepository _progressRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly ICacheService _cache;

    public GetWeakConceptsQueryHandler(
        IUserConceptProgressRepository progressRepository,
        IConceptRepository conceptRepository,
        ICacheService cache)
    {
        _progressRepository = progressRepository;
        _conceptRepository = conceptRepository;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<WeakConceptItem>>> Handle(GetWeakConceptsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"weak-topics:{request.UserId}";
        var cached = await _cache.GetAsync<IReadOnlyList<WeakConceptItem>>(cacheKey, cancellationToken);
        if (cached != null)
            return Result<IReadOnlyList<WeakConceptItem>>.Success(cached);
        var weakProgressList = await _progressRepository.GetWeakByUserIdAsync(request.UserId, WeakThreshold, cancellationToken);
        if (weakProgressList.Count == 0)
            return Result<IReadOnlyList<WeakConceptItem>>.Success(Array.Empty<WeakConceptItem>());

        var conceptIds = weakProgressList.Select(p => p.ConceptId).Distinct().ToList();
        var concepts = await _conceptRepository.GetByIdsAsync(conceptIds, cancellationToken);
        var conceptMap = concepts.ToDictionary(c => c.Id);

        var items = weakProgressList
            .Where(p => conceptMap.TryGetValue(p.ConceptId, out _))
            .Select(p =>
            {
                var concept = conceptMap[p.ConceptId];
                var name = concept.Name ?? string.Empty;
                return new WeakConceptItem(p.ConceptId, name, p.MasteryScore.Value);
            })
            .ToList();

        await _cache.SetAsync(cacheKey, items, CacheTtl, cancellationToken);
        return Result<IReadOnlyList<WeakConceptItem>>.Success(items);
    }
}
