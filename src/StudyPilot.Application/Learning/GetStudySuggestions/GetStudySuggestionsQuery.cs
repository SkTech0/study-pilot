using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Learning.GetStudySuggestions;

public sealed record GetStudySuggestionsQuery(Guid UserId) : IRequest<Result<StudySuggestionsResult>>;

public sealed record StudySuggestionsResult(IReadOnlyList<StudySuggestionItem> Suggestions);

public sealed record StudySuggestionItem(string Title, string Description, Guid? DocumentId);
