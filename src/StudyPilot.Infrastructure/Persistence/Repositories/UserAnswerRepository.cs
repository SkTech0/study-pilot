using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class UserAnswerRepository : IUserAnswerRepository
{
    private readonly StudyPilotDbContext _db;

    public UserAnswerRepository(StudyPilotDbContext db) => _db = db;

    public async Task AddAsync(UserAnswer answer, CancellationToken cancellationToken = default) =>
        await _db.UserAnswers.AddAsync(answer, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<UserAnswer> answers, CancellationToken cancellationToken = default) =>
        await _db.UserAnswers.AddRangeAsync(answers, cancellationToken);
}
