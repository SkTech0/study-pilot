using System.Text;
using System.Text.RegularExpressions;

namespace StudyPilot.Infrastructure.Knowledge.Chunking;

internal static class TextChunker
{
    private static readonly Regex ParagraphSplit = new(@"\n\s*\n+", RegexOptions.Compiled);
    private static readonly Regex SentenceSplit = new(@"(?<=[\.!\?])\s+", RegexOptions.Compiled);

    public static IReadOnlyList<TextChunk> Chunk(string text, int targetTokens = 800, int overlapTokens = 150, int maxChunks = 2000)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<TextChunk>();
        var normalized = Normalize(text);
        var paragraphs = ParagraphSplit.Split(normalized).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var chunks = new List<TextChunk>();

        var carryOver = "";
        foreach (var para in paragraphs)
        {
            var sentences = SentenceSplit.Split(para).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(carryOver))
            {
                sb.Append(carryOver);
                if (!carryOver.EndsWith(' ')) sb.Append(' ');
            }

            foreach (var sentence in sentences)
            {
                var candidate = sb.Length == 0 ? sentence : $"{sb} {sentence}";
                var candidateTokens = EstimateTokens(candidate);
                if (candidateTokens <= targetTokens)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(sentence);
                    continue;
                }

                if (sb.Length > 0)
                {
                    var chunkText = sb.ToString().Trim();
                    var tokenCount = EstimateTokens(chunkText);
                    chunks.Add(new TextChunk(chunkText, tokenCount));
                    if (chunks.Count >= maxChunks) return chunks;
                    carryOver = BuildOverlap(chunkText, overlapTokens);
                }

                sb.Clear();
                if (!string.IsNullOrEmpty(carryOver))
                {
                    sb.Append(carryOver);
                    if (!carryOver.EndsWith(' ')) sb.Append(' ');
                }

                // If the sentence itself is too large, split by words.
                if (EstimateTokens(sentence) > targetTokens)
                {
                    foreach (var part in SplitLargeSentence(sentence, targetTokens))
                    {
                        var partText = sb.Length == 0 ? part : $"{sb} {part}";
                        if (EstimateTokens(partText) > targetTokens && sb.Length > 0)
                        {
                            var flushText = sb.ToString().Trim();
                            chunks.Add(new TextChunk(flushText, EstimateTokens(flushText)));
                            if (chunks.Count >= maxChunks) return chunks;
                            carryOver = BuildOverlap(flushText, overlapTokens);
                            sb.Clear();
                            if (!string.IsNullOrEmpty(carryOver))
                            {
                                sb.Append(carryOver);
                                if (!carryOver.EndsWith(' ')) sb.Append(' ');
                            }
                        }
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(part);
                    }
                }
                else
                {
                    sb.Append(sentence);
                }
            }

            if (sb.Length > 0)
            {
                var chunkText = sb.ToString().Trim();
                chunks.Add(new TextChunk(chunkText, EstimateTokens(chunkText)));
                if (chunks.Count >= maxChunks) return chunks;
                carryOver = BuildOverlap(chunkText, overlapTokens);
            }
        }

        return chunks;
    }

    private static IEnumerable<string> SplitLargeSentence(string sentence, int targetTokens)
    {
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) yield break;

        var sb = new StringBuilder();
        foreach (var w in words)
        {
            var candidate = sb.Length == 0 ? w : $"{sb} {w}";
            if (EstimateTokens(candidate) <= targetTokens)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(w);
                continue;
            }
            if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            sb.Append(w);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static string BuildOverlap(string chunkText, int overlapTokens)
    {
        if (overlapTokens <= 0) return "";
        var words = chunkText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return "";
        var start = Math.Max(0, words.Length - overlapTokens);
        return string.Join(' ', words.Skip(start));
    }

    private static int EstimateTokens(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        // Heuristic: token ~= word; good enough for consistent chunk sizing.
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length;
    }

    private static string Normalize(string text)
    {
        var t = text.Replace("\r\n", "\n").Replace('\r', '\n');
        // Preserve newlines for paragraph boundaries; normalize horizontal whitespace.
        t = t.Replace('\t', ' ');
        t = Regex.Replace(t, @"[ ]{2,}", " ");
        t = Regex.Replace(t, @" *\n *", "\n");
        t = Regex.Replace(t, @"\n{3,}", "\n\n");
        return t.Trim();
    }

    internal sealed record TextChunk(string Text, int TokenCount);
}

