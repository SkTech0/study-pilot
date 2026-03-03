using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class TutorExerciseRepository : ITutorExerciseRepository
{
    private readonly StudyPilotDbContext _db;

    public TutorExerciseRepository(StudyPilotDbContext db) => _db = db;

    public async Task<TutorExercise?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.TutorExercises.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(TutorExercise exercise, CancellationToken cancellationToken = default) =>
        await _db.TutorExercises.AddAsync(exercise, cancellationToken);
}
