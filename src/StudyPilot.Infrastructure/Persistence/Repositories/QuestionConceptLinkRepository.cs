using Microsoft.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;

namespace StudyPilot.Infrastructure.Persistence.Repositories;

public sealed class QuestionConceptLinkRepository : IQuestionConceptLinkRepository
{
    private readonly StudyPilotDbContext _db;

    public QuestionConceptLinkRepository(StudyPilotDbContext db) => _db = db;

    public async Task<Guid?> GetConceptIdForQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var link = await _db.QuestionConceptLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.QuestionId == questionId, cancellationToken);
        return link?.ConceptId;
    }

    public async Task AddAsync(Guid questionId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        await _db.QuestionConceptLinks.AddAsync(new QuestionConceptLink { QuestionId = questionId, ConceptId = conceptId }, cancellationToken);
    }
}
