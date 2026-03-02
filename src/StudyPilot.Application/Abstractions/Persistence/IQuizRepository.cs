namespace StudyPilot.Application.Abstractions.Persistence;

public interface IQuizRepository
{
    Task<Domain.Entities.Quiz?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Domain.Entities.Quiz?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Domain.Entities.Quiz quiz, CancellationToken cancellationToken = default);
    Task UpdateAsync(Domain.Entities.Quiz quiz, CancellationToken cancellationToken = default);
}
