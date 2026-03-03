using StudyPilot.Application.Abstractions.Tutor;
using StudyPilot.Application.Tutor.Models;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.AI;

namespace StudyPilot.Infrastructure.Tutor;

public sealed class TutorService : ITutorService
{
    private readonly IStudyPilotKnowledgeAIClient _client;

    public TutorService(IStudyPilotKnowledgeAIClient client) => _client = client;

    public async Task<TutorResponse> RespondAsync(TutorContext context, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(context);
        var response = await _client.TutorRespondAsync(dto, cancellationToken);
        return new TutorResponse(
            response.Message ?? "",
            Enum.TryParse<TutorStep>(response.NextStep, ignoreCase: true, out var step) ? step : TutorStep.Diagnose,
            response.OptionalExercise is null ? null : new TutorExerciseInfo(response.OptionalExercise.Question ?? "", response.OptionalExercise.ExpectedAnswer ?? "", response.OptionalExercise.Difficulty ?? "medium"),
            response.CitedChunkIds?.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList() ?? new List<Guid>());
    }

    public async Task<TutorStreamResult> StreamRespondAsync(TutorContext context, Func<string, Task> onToken, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(context);
        var result = await _client.TutorStreamRespondAsync(dto, onToken, cancellationToken);
        return new TutorStreamResult(
            Enum.TryParse<TutorStep>(result.NextStep, ignoreCase: true, out var step) ? step : TutorStep.Diagnose,
            result.OptionalExercise is null ? null : new TutorExerciseInfo(result.OptionalExercise.Question ?? "", result.OptionalExercise.ExpectedAnswer ?? "", result.OptionalExercise.Difficulty ?? "medium"),
            result.CitedChunkIds?.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList() ?? new List<Guid>());
    }

    public async Task<ExerciseEvaluationResult> EvaluateExerciseAsync(ExerciseEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var dto = new ExerciseEvaluationRequestDto
        {
            ExerciseId = request.ExerciseId.ToString(),
            Question = request.Question,
            ExpectedAnswer = request.ExpectedAnswer,
            UserAnswer = request.UserAnswer
        };
        var result = await _client.EvaluateExerciseAsync(dto, cancellationToken);
        return new ExerciseEvaluationResult(result.IsCorrect, result.Explanation ?? "");
    }

    private static TutorContextDto ToDto(TutorContext context)
    {
        return new TutorContextDto
        {
            UserId = context.UserId.ToString(),
            TutorSessionId = context.TutorSessionId.ToString(),
            UserMessage = context.UserMessage,
            CurrentStep = context.CurrentStep.ToString(),
            Goals = context.Goals.Select(g => new TutorGoalDto
            {
                GoalId = g.GoalId.ToString(),
                ConceptId = g.ConceptId.ToString(),
                ConceptName = g.ConceptName,
                GoalType = g.GoalType,
                ProgressPercent = g.ProgressPercent
            }).ToList(),
            MasteryLevels = context.MasteryLevels.Select(m => new TutorMasteryDto
            {
                ConceptId = m.ConceptId.ToString(),
                ConceptName = m.ConceptName,
                MasteryScore = m.MasteryScore
            }).ToList(),
            RecentMistakes = context.RecentMistakes?.ToList() ?? new List<string>(),
            ExplanationStyle = context.ExplanationStyle,
            Tone = context.Tone.ToString(),
            RetrievedChunks = context.RetrievedChunks.Select(c => new TutorChunkDto
            {
                ChunkId = c.ChunkId.ToString(),
                DocumentId = c.DocumentId.ToString(),
                Text = c.Text
            }).ToList()
        };
    }
}
