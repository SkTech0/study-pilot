namespace StudyPilot.Application.Abstractions.Learning;

/// <summary>
/// Deterministic mastery scoring: updates UserConceptMastery from quiz results, chat, and time decay.
/// </summary>
public interface IMasteryEngine
{
    Task UpdateFromQuizResultAsync(QuizResultForMastery result, CancellationToken cancellationToken = default);
    Task UpdateFromChatInteractionAsync(ChatInteractionForMastery interaction, CancellationToken cancellationToken = default);
    Task ApplyTimeDecayAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record QuizResultForMastery(
    Guid UserId,
    IReadOnlyList<ConceptAnswerResult> ConceptResults);

public sealed record ConceptAnswerResult(Guid ConceptId, bool IsCorrect);

public sealed record ChatInteractionForMastery(Guid UserId, Guid ConceptId, bool WasClarification);
