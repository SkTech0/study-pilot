namespace StudyPilot.Application.Knowledge.Models;

public sealed record ChatRequest(
    Guid UserId,
    Guid SessionId,
    Guid? DocumentId,
    string Question,
    IReadOnlyList<RetrievedChunk> ContextChunks,
    string SystemInstruction);

