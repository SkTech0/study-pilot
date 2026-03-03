namespace StudyPilot.API.Contracts.Responses;

public sealed record StudySuggestionsResponse(IReadOnlyList<StudySuggestionItemResponse> Suggestions);

public sealed record StudySuggestionItemResponse(string Title, string Description, Guid? DocumentId);
