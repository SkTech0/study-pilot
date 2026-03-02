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
        // Keep payload small to reduce token usage and rate-limit risk on free-tier LLMs.
        var selectedConcepts = concepts
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Take(8)
            .ToList();
        var names = selectedConcepts.Select(c => c.Name).ToList();
        var count = Math.Min(5, Math.Max(1, names.Count));
        var result = await _client.GenerateQuizAsync(documentId, names, count, cancellationToken);
        var conceptIds = selectedConcepts.Select(c => c.Id).ToList();
        var questions = result.Questions
            .Where(q => !string.IsNullOrWhiteSpace(q.CorrectAnswer))
            .Select((q, i) => new GeneratedQuestion(
                q.Text,
                QuestionType.MCQ,
                (q.CorrectAnswer ?? "").Trim(),
                q.Options,
                i < conceptIds.Count ? conceptIds[i] : Guid.Empty,
                result.PromptVersion,
                result.ModelName,
                result.Temperature,
                result.TokenUsage))
            .ToList();
        return new GenerateQuizResult(questions);
    }

    public async Task<GeneratedQuestion?> GenerateQuestionAsync(Guid documentId, Guid userId, ConceptInfo concept, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(concept.Name))
            return null;
        var result = await _client.GenerateQuizAsync(documentId, new[] { concept.Name }, 1, cancellationToken);
        var q = result.Questions?.FirstOrDefault();
        if (q is null || string.IsNullOrWhiteSpace(q.CorrectAnswer))
            return null;
        return new GeneratedQuestion(
            q.Text,
            QuestionType.MCQ,
            (q.CorrectAnswer ?? "").Trim(),
            q.Options ?? new List<string>(),
            concept.Id,
            result.PromptVersion,
            result.ModelName,
            result.Temperature,
            result.TokenUsage);
    }
}
