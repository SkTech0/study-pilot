using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StudyPilot.Domain.ValueObjects;

namespace StudyPilot.Infrastructure.Persistence.ValueConverters;

/// <summary>
/// EF Core value converter for Email value object. Ensures the database string is always
/// converted via Email.TryCreate/Create so no direct cast from string to Email can occur.
/// </summary>
public sealed class EmailValueConverter : ValueConverter<Email, string>
{
    public EmailValueConverter()
        : base(
            e => e.Value,
            s => FromProvider(s))
    {
    }

    private static Email FromProvider(string? value)
    {
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException("Email column cannot be null or empty.");
        if (!Email.TryCreate(value, out var email, out var error))
            throw new InvalidOperationException($"Stored email value is invalid: {error}");
        return email!;
    }
}
