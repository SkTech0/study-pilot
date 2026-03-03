namespace StudyPilot.Application.Abstractions.Persistence;

/// <summary>
/// Adaptive quiz: stores ordered concept IDs per quiz (50% weak, 30% medium, 20% strong).
/// </summary>
public interface IQuizConceptOrderRepository
{
    Task<IReadOnlyList<Guid>> GetConceptIdsForQuizAsync(Guid quizId, CancellationToken cancellationToken = default);
    Task SetConceptOrderAsync(Guid quizId, IReadOnlyList<Guid> conceptIds, CancellationToken cancellationToken = default);
}
