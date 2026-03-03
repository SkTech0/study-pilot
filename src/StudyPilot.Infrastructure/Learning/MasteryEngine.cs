using StudyPilot.Application.Abstractions.Learning;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Domain.Entities;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.Learning;

public sealed class MasteryEngine : IMasteryEngine
{
    private const int CorrectDelta = 8;
    private const int WrongDecay = 5;
    private const double TimeDecayPerDay = 0.5;

    private readonly IUserConceptMasteryRepository _masteryRepo;
    private readonly IUnitOfWork _unitOfWork;

    public MasteryEngine(IUserConceptMasteryRepository masteryRepo, IUnitOfWork unitOfWork)
    {
        _masteryRepo = masteryRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task UpdateFromQuizResultAsync(QuizResultForMastery result, CancellationToken cancellationToken = default)
    {
        foreach (var cr in result.ConceptResults)
        {
            var entity = await _masteryRepo.GetByUserAndConceptAsync(result.UserId, cr.ConceptId, cancellationToken).ConfigureAwait(false);
            var isNew = entity is null;
            if (entity is null)
            {
                entity = new UserConceptMastery(result.UserId, cr.ConceptId, 50, 0.3);
                await _masteryRepo.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            }
            if (cr.IsCorrect)
                entity.ApplyCorrectAnswer(CorrectDelta);
            else
                entity.ApplyWrongAnswer(WrongDecay);
            if (!isNew)
                await _masteryRepo.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        StudyPilotMetrics.MasteryUpdates.Add(result.ConceptResults.Count);
    }

    public async Task UpdateFromChatInteractionAsync(ChatInteractionForMastery interaction, CancellationToken cancellationToken = default)
    {
        var entity = await _masteryRepo.GetByUserAndConceptAsync(interaction.UserId, interaction.ConceptId, cancellationToken).ConfigureAwait(false);
        var isNew = entity is null;
        if (entity is null)
        {
            entity = new UserConceptMastery(interaction.UserId, interaction.ConceptId, 50, 0.2);
            await _masteryRepo.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        if (interaction.WasClarification)
            entity.ApplyWrongAnswer(2);
        if (!isNew)
            await _masteryRepo.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        StudyPilotMetrics.MasteryUpdates.Add(1);
    }

    public async Task ApplyTimeDecayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var list = await _masteryRepo.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (var entity in list)
        {
            entity.ApplyTimeDecay(TimeDecayPerDay);
            await _masteryRepo.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        if (list.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            StudyPilotMetrics.MasteryUpdates.Add(list.Count);
        }
    }
}
