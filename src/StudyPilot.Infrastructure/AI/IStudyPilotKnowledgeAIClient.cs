namespace StudyPilot.Infrastructure.AI;

public interface IStudyPilotKnowledgeAIClient
{
    Task<EmbeddingsResultDto> CreateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    Task<ChatResultDto> ChatAsync(ChatRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Streams chat tokens from Python /chat/stream; onToken is invoked for each token; returns metadata on done.
    /// </summary>
    Task<StreamChatResultDto> StreamChatAsync(
        ChatRequestDto request,
        Func<string, Task> onToken,
        CancellationToken ct = default);

    Task<TutorResponseDto> TutorRespondAsync(TutorContextDto request, CancellationToken ct = default);
    Task<TutorStreamResultDto> TutorStreamRespondAsync(TutorContextDto request, Func<string, Task> onToken, CancellationToken ct = default);
    Task<ExerciseEvaluationResultDto> EvaluateExerciseAsync(ExerciseEvaluationRequestDto request, CancellationToken ct = default);
}

public sealed class TutorContextDto
{
    public string UserId { get; set; } = "";
    public string TutorSessionId { get; set; } = "";
    public string UserMessage { get; set; } = "";
    public string CurrentStep { get; set; } = "";
    public List<TutorGoalDto> Goals { get; set; } = [];
    public List<TutorMasteryDto> MasteryLevels { get; set; } = [];
    public List<string> RecentMistakes { get; set; } = [];
    public string? ExplanationStyle { get; set; }
    public string? Tone { get; set; }
    public List<TutorChunkDto> RetrievedChunks { get; set; } = [];
}

public sealed class TutorGoalDto
{
    public string GoalId { get; set; } = "";
    public string ConceptId { get; set; } = "";
    public string ConceptName { get; set; } = "";
    public string GoalType { get; set; } = "";
    public int ProgressPercent { get; set; }
}

public sealed class TutorMasteryDto
{
    public string ConceptId { get; set; } = "";
    public string ConceptName { get; set; } = "";
    public int MasteryScore { get; set; }
}

public sealed class TutorChunkDto
{
    public string ChunkId { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class TutorResponseDto
{
    public string Message { get; set; } = "";
    public string NextStep { get; set; } = "";
    public TutorExerciseDto? OptionalExercise { get; set; }
    public List<string> CitedChunkIds { get; set; } = [];
}

public sealed class TutorExerciseDto
{
    public string Question { get; set; } = "";
    public string ExpectedAnswer { get; set; } = "";
    public string Difficulty { get; set; } = "medium";
}

public sealed class TutorStreamResultDto
{
    public string NextStep { get; set; } = "";
    public TutorExerciseDto? OptionalExercise { get; set; }
    public List<string> CitedChunkIds { get; set; } = [];
}

public sealed class ExerciseEvaluationRequestDto
{
    public string ExerciseId { get; set; } = "";
    public string Question { get; set; } = "";
    public string ExpectedAnswer { get; set; } = "";
    public string UserAnswer { get; set; } = "";
}

public sealed class ExerciseEvaluationResultDto
{
    public bool IsCorrect { get; set; }
    public string Explanation { get; set; } = "";
}

public sealed class StreamChatResultDto
{
    public List<string> CitedChunkIds { get; set; } = [];
    public string? Model { get; set; }
    public bool FallbackUsed { get; set; }
}

public sealed class EmbeddingsResultDto
{
    public List<float[]> Embeddings { get; set; } = [];
    public string? Model { get; set; }
}

public sealed class ChatRequestDto
{
    public string SessionId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? DocumentId { get; set; }
    public string System { get; set; } = "";
    public string Question { get; set; } = "";
    public List<ChatContextChunkDto> Context { get; set; } = [];
    /// <summary>Beginner | Intermediate | Advanced - adjusts explanation style.</summary>
    public string? ExplanationStyle { get; set; }
}

public sealed class ChatContextChunkDto
{
    public string ChunkId { get; set; } = "";
    public string Text { get; set; } = "";
    public string DocumentId { get; set; } = "";
}

public sealed class ChatResultDto
{
    public string Answer { get; set; } = "";
    public List<string> CitedChunkIds { get; set; } = [];
    public string? Model { get; set; }
}

