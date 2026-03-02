using StudyPilot.Domain.Common;
using StudyPilot.Domain.Enums;
using StudyPilot.Domain.ValueObjects;

namespace StudyPilot.Domain.Entities;

public class User : BaseEntity
{
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }

    private readonly List<Document> _documents = new();
    public IReadOnlyCollection<Document> Documents => _documents.AsReadOnly();

    private User() : base()
    {
    }

    public User(Email email, string passwordHash, UserRole role) : base()
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        PasswordHash = string.IsNullOrEmpty(passwordHash)
            ? throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash))
            : passwordHash;
        Role = role;
    }

    public User(Guid id, string emailValue, string passwordHash, UserRole role, DateTime createdAtUtc, DateTime updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
        Email = Email.Create(emailValue);
        PasswordHash = passwordHash;
        Role = role;
    }

    public void AddDocument(Document document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (document.UserId != Id)
            throw new InvalidOperationException("Document must belong to this user.");
        _documents.Add(document);
        Touch();
    }
}
