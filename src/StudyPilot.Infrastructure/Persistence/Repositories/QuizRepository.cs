using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class QuizRepository : IQuizRepository
{
    private readonly StudyPilotDbContext _db;

    public QuizRepository(StudyPilotDbContext db) => _db = db;

    public async Task<Quiz?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Quizzes
            .Include(q => q.Questions)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<Quiz?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task AddAsync(Quiz quiz, CancellationToken cancellationToken = default)
    {
        await _db.Quizzes.AddAsync(quiz, cancellationToken);
        if (quiz.Questions.Count != 0)
            await _db.Questions.AddRangeAsync(quiz.Questions.ToList(), cancellationToken);
    }

    public Task UpdateAsync(Quiz quiz, CancellationToken cancellationToken = default)
    {
        _db.Quizzes.Update(quiz);
        return Task.CompletedTask;
    }
}
