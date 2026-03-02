namespace StudyPilot.Application.Abstractions.Persistence;

public interface IQuestionConceptLinkRepository
{
    Task<Guid?> GetConceptIdForQuestionAsync(Guid questionId, CancellationToken cancellationToken = default);
    Task AddAsync(Guid questionId, Guid conceptId, CancellationToken cancellationToken = default);
}