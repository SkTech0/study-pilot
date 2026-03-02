using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StudyPilot.Domain.ValueObjects;

namespace StudyPilot.Infrastructure.Persistence.ValueConverters;

/// <summary>
/// EF Core value converter for MasteryScore value object. Ensures the database int is always
/// converted via MasteryScore.Create so no direct cast from int to MasteryScore can occur.
/// </summary>
public sealed class MasteryScoreValueConverter : ValueConverter<MasteryScore, int>
{
    public MasteryScoreValueConverter()
        : base(
            s => s.Value,
            i => MasteryScore.Create(i))
    {
    }
}
