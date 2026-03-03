using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface ITutorExerciseRepository
{
    Task<TutorExercise?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TutorExercise exercise, CancellationToken cancellationToken = default);
}
