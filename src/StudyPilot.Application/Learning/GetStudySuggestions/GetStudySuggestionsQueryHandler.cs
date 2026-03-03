using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetStudySuggestions;

public sealed class GetStudySuggestionsQueryHandler : IRequestHandler<GetStudySuggestionsQuery, Result<StudySuggestionsResult>>
{
    private const int WeakThreshold = 40;
    private const int MaxSuggestions = 5;

    private readonly IUserConceptMasteryRepository _masteryRepository;
    private readonly IConceptRepository _conceptRepository;

    public GetStudySuggestionsQueryHandler(
        IUserConceptMasteryRepository masteryRepository,
        IConceptRepository conceptRepository)
    {
        _masteryRepository = masteryRepository;
        _conceptRepository = conceptRepository;
    }

    public async Task<Result<StudySuggestionsResult>> Handle(GetStudySuggestionsQuery request, CancellationToken cancellationToken)
    {
        var list = await _masteryRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var weak = list.Where(m => m.MasteryScore <= WeakThreshold).OrderBy(m => m.MasteryScore).Take(MaxSuggestions).ToList();
        if (weak.Count == 0)
            return Result<StudySuggestionsResult>.Success(new StudySuggestionsResult(Array.Empty<StudySuggestionItem>()));

        var conceptIds = weak.Select(m => m.ConceptId).Distinct().ToList();
        var concepts = await _conceptRepository.GetByIdsAsync(conceptIds, cancellationToken);
        var conceptMap = concepts.ToDictionary(c => c.Id);

        var names = weak.Where(m => conceptMap.TryGetValue(m.ConceptId, out _)).Select(m => conceptMap[m.ConceptId].Name).Take(3).ToList();
        var description = names.Count > 0
            ? $"Focus on: {string.Join(", ", names)}."
            : "Review your weak topics from recent quizzes.";
        var suggestions = new List<StudySuggestionItem>
        {
            new StudySuggestionItem("Recommended next study session", description, null)
        };
        return Result<StudySuggestionsResult>.Success(new StudySuggestionsResult(suggestions));
    }
}
