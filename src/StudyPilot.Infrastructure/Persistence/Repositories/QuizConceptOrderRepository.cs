using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class QuizConceptOrderRepository : IQuizConceptOrderRepository
{
    private readonly StudyPilotDbContext _db;

    public QuizConceptOrderRepository(StudyPilotDbContext db) => _db = db;

    public async Task<IReadOnlyList<Guid>> GetConceptIdsForQuizAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.QuizConceptOrders
            .AsNoTracking()
            .Where(o => o.QuizId == quizId)
            .OrderBy(o => o.QuestionIndex)
            .Select(o => o.ConceptId)
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task SetConceptOrderAsync(Guid quizId, IReadOnlyList<Guid> conceptIds, CancellationToken cancellationToken = default)
    {
        var existing = await _db.QuizConceptOrders.Where(o => o.QuizId == quizId).ToListAsync(cancellationToken);
        _db.QuizConceptOrders.RemoveRange(existing);
        for (var i = 0; i < conceptIds.Count; i++)
            await _db.QuizConceptOrders.AddAsync(new QuizConceptOrder { QuizId = quizId, QuestionIndex = i, ConceptId = conceptIds[i] }, cancellationToken);
        if (conceptIds.Count > 0)
            StudyPilotMetrics.AdaptiveQuizUsage.Add(1);
    }
}
