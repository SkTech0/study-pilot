namespace StudyPilot.Application.Knowledge.Models;

public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string Text,
    int TokenCount,
    double Score);

