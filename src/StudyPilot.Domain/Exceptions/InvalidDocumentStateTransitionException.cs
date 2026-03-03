namespace StudyPilot.Domain.Exceptions;

/// <summary>
/// Thrown when an invalid document lifecycle transition is attempted.
/// All status changes must go through the knowledge state machine and respect DocumentKnowledgePolicy.
/// </summary>
public sealed class InvalidDocumentStateTransitionException : InvalidOperationException
{
    public InvalidDocumentStateTransitionException(string message) : base(message) { }
    public InvalidDocumentStateTransitionException(string message, Exception inner) : base(message, inner) { }
}
