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

    public async Task<IReadOnlyList<Question>> GetQuestionsByQuizIdAsync(Guid quizId, CancellationToken cancellationToken = default) =>
        await _db.Questions
            .AsNoTracking()
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.QuestionIndex)
            .ToListAsync(cancellationToken);

    public async Task<Quiz?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<Question?> GetQuestionByQuizAndIndexAsync(Guid quizId, int questionIndex, CancellationToken cancellationToken = default) =>
        await _db.Questions
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.QuestionIndex == questionIndex, cancellationToken);

    public async Task AddAsync(Quiz quiz, CancellationToken cancellationToken = default)
    {
        await _db.Quizzes.AddAsync(quiz, cancellationToken);
        if (quiz.Questions.Count != 0)
            await _db.Questions.AddRangeAsync(quiz.Questions.ToList(), cancellationToken);
    }

    public async Task<bool> TryAddQuestionAsync(Question question, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Questions.AddAsync(question, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("23505", StringComparison.OrdinalIgnoreCase);
    }

    public async Task AddQuestionAsync(Question question, CancellationToken cancellationToken = default)
    {
        await _db.Questions.AddAsync(question, cancellationToken);
    }

    public Task UpdateAsync(Quiz quiz, CancellationToken cancellationToken = default)
    {
        _db.Quizzes.Update(quiz);
        return Task.CompletedTask;
    }

    public Task UpdateQuestionAsync(Question question, CancellationToken cancellationToken = default)
    {
        _db.Questions.Update(question);
        return Task.CompletedTask;
    }
}
