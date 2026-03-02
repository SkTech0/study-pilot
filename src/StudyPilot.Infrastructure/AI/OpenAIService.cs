using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Infrastructure.AI;

public sealed class OpenAIService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAIServiceOptions _options;

    public OpenAIService(IHttpClientFactory httpClientFactory, IOptions<OpenAIServiceOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<ExtractConceptsResult> ExtractConceptsAsync(Guid documentId, string contentOrPath, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var mock = new List<ExtractedConceptItem>
        {
            new("Concept One", "First extracted concept"),
            new("Concept Two", "Second extracted concept"),
            new("Concept Three", null)
        };
        return new ExtractConceptsResult(mock);
    }

    public async Task<GenerateQuizResult> GenerateQuizAsync(Guid documentId, Guid userId, IReadOnlyList<ConceptInfo> concepts, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (concepts.Count == 0)
            return new GenerateQuizResult([]);

        var questions = concepts.Take(3).Select((c, i) => new GeneratedQuestion(
            $"Sample question {i + 1} about {c.Name}?",
            QuestionType.MCQ,
            "Correct",
            ["Correct", "Wrong", "Also wrong"],
            c.Id
        )).ToList();
        return new GenerateQuizResult(questions);
    }

    public async Task<GeneratedQuestion?> GenerateQuestionAsync(Guid documentId, Guid userId, ConceptInfo concept, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return new GeneratedQuestion(
            $"Sample question about {concept.Name}?",
            QuestionType.MCQ,
            "Correct",
            ["Correct", "Wrong", "Also wrong"],
            concept.Id);
    }
}
