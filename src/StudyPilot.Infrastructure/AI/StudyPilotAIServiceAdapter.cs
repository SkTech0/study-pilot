using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.AI;

public sealed class StudyPilotAIServiceAdapter : IAIService
{
    private readonly IStudyPilotAIClient _client;

    public StudyPilotAIServiceAdapter(IStudyPilotAIClient client) => _client = client;

    public async Task<ExtractConceptsResult> ExtractConceptsAsync(Guid documentId, string contentOrPath, CancellationToken cancellationToken = default)
    {
        var concepts = await _client.ExtractConceptsAsync(documentId, contentOrPath, cancellationToken);
        var items = concepts.Select(c => new ExtractedConceptItem(c.Name, c.Description)).ToList();
        return new ExtractConceptsResult(items);
    }

    public async Task<GenerateQuizResult> GenerateQuizAsync(Guid documentId, Guid userId, IReadOnlyList<ConceptInfo> concepts, CancellationToken cancellationToken = default)
    {
        var names = concepts.Select(c => c.Name).ToList();
        var count = Math.Min(10, Math.Max(1, concepts.Count));
        var result = await _client.GenerateQuizAsync(documentId, names, count, cancellationToken);
        var conceptIds = concepts.Select(c => c.Id).ToList();
        var questions = result.Questions
            .Select((q, i) => new GeneratedQuestion(
                q.Text,
                QuestionType.MCQ,
                q.CorrectAnswer,
                q.Options,
                i < conceptIds.Count ? conceptIds[i] : Guid.Empty))
            .ToList();
        return new GenerateQuizResult(questions);
    }
}
