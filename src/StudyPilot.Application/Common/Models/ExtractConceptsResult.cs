namespace StudyPilot.Application.Common.Models;

public sealed record ExtractConceptsResult(IReadOnlyList<ExtractedConceptItem> Concepts);
