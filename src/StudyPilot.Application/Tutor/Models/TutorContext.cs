using StudyPilot.Application.Knowledge.Models;
using StudyPilot.Domain.Enums;

namespace StudyPilot.Application.Tutor.Models;

public sealed record TutorContext(
    Guid UserId,
    Guid TutorSessionId,
    string UserMessage,
    TutorStep CurrentStep,
    IReadOnlyList<TutorGoalInfo> Goals,
    IReadOnlyList<TutorMasteryInfo> MasteryLevels,
    IReadOnlyList<string> RecentMistakes,
    string ExplanationStyle,
    TutorTone Tone,
    IReadOnlyList<TutorContextChunk> RetrievedChunks);

public sealed record TutorGoalInfo(Guid GoalId, Guid ConceptId, string ConceptName, string GoalType, int ProgressPercent);

public sealed record TutorMasteryInfo(Guid ConceptId, string ConceptName, int MasteryScore);

public sealed record TutorContextChunk(Guid ChunkId, Guid DocumentId, string Text);
