namespace StudyPilot.Application.Knowledge.Constants;

/// <summary>
/// Context safety: minimum chunks and similarity threshold to allow AI answer.
/// Below these we return a controlled "not enough information" response.
/// </summary>
public static class RetrievalConstants
{
    public const int MinimumChunksForAnswer = 3;
    /// <summary>
    /// Minimum acceptable similarity score (cosine; higher = more similar).
    /// pgvector <=> returns distance; we use 1 - distance as similarity in search.
    /// Typical good matches are &lt; 0.5 distance (similarity &gt; 0.5).
    /// </summary>
    public const double MinimumSimilarityThreshold = 0.35;

    public const string InsufficientContextMessage =
        "I couldn't find enough information in your documents to answer confidently.";

    /// <summary>Hint when embeddings may be delayed (e.g. document just processed).</summary>
    public const string EmbeddingsDelayedRetryHint =
        " If the document was recently uploaded, wait a moment and try again.";
}
