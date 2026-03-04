namespace StudyPilot.Application.Chat;

/// <summary>Chat response status for frontend contract and observability.</summary>
public enum ChatStatus
{
    Ok,
    Fallback,
    InsufficientContext,
    Error
}

public static class ChatStatusExtensions
{
    /// <summary>API contract: "ok" | "fallback" | "insufficient_context" | "error".</summary>
    public static string ToApiString(this ChatStatus status) => status switch
    {
        ChatStatus.Ok => "ok",
        ChatStatus.Fallback => "fallback",
        ChatStatus.InsufficientContext => "insufficient_context",
        ChatStatus.Error => "error",
        _ => "error"
    };
}
