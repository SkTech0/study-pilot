using System.Text.RegularExpressions;

namespace StudyPilot.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    private static readonly Regex Format = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (!TryCreate(value, out var email, out _))
            throw new ArgumentException("Invalid email.", nameof(value));
        return email!;
    }

    /// <summary>
    /// Attempts to create an Email from a string. Use this for validation without throwing.
    /// </summary>
    public static bool TryCreate(string? value, out Email? email, out string? errorMessage)
    {
        email = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Email cannot be empty.";
            return false;
        }
        var trimmed = value.Trim();
        if (!Format.IsMatch(trimmed))
        {
            errorMessage = "Invalid email format.";
            return false;
        }
        if (trimmed.Length > 320)
        {
            errorMessage = "Email must not exceed 320 characters.";
            return false;
        }
        email = new Email(trimmed);
        return true;
    }

    public bool Equals(Email? other) => other is not null && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => obj is Email other && Equals(other);
    public override int GetHashCode() => Value.ToUpperInvariant().GetHashCode();
    public override string ToString() => Value;
}
