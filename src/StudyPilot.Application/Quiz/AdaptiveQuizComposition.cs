namespace StudyPilot.Application.Quiz;

/// <summary>
/// Classifies mastery into Weak (0-40), Medium (41-70), Strong (71-100).
/// Builds ordered concept list: 50% weak, 30% medium, 20% strong.
/// </summary>
public static class AdaptiveQuizComposition
{
    public const int WeakMax = 40;
    public const int MediumMax = 70;
    public const double WeakFraction = 0.5;
    public const double MediumFraction = 0.3;
    public const double StrongFraction = 0.2;

    public static IReadOnlyList<Guid> BuildOrderedConceptIds(
        IReadOnlyList<ConceptWithMastery> concepts,
        int totalSlots)
    {
        if (concepts.Count == 0 || totalSlots <= 0) return Array.Empty<Guid>();

        var weak = new List<Guid>();
        var medium = new List<Guid>();
        var strong = new List<Guid>();

        foreach (var c in concepts)
        {
            if (c.MasteryScore <= WeakMax) weak.Add(c.ConceptId);
            else if (c.MasteryScore <= MediumMax) medium.Add(c.ConceptId);
            else strong.Add(c.ConceptId);
        }

        var weakCount = Math.Max(0, (int)Math.Round(totalSlots * WeakFraction));
        var mediumCount = Math.Max(0, (int)Math.Round(totalSlots * MediumFraction));
        var strongCount = Math.Max(0, totalSlots - weakCount - mediumCount);

        var ordered = new List<Guid>();
        ordered.AddRange(TakeShuffled(weak, weakCount));
        ordered.AddRange(TakeShuffled(medium, mediumCount));
        ordered.AddRange(TakeShuffled(strong, strongCount));

        if (ordered.Count < totalSlots)
        {
            var remaining = concepts.Select(c => c.ConceptId).Except(ordered).ToList();
            var rng = new Random();
            while (ordered.Count < totalSlots && remaining.Count > 0)
            {
                var idx = rng.Next(remaining.Count);
                ordered.Add(remaining[idx]);
                remaining.RemoveAt(idx);
            }
        }

        return ordered.Take(totalSlots).ToList();
    }

    private static List<Guid> TakeShuffled(List<Guid> source, int count)
    {
        if (source.Count == 0 || count <= 0) return new List<Guid>();
        var shuffled = source.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
        return shuffled;
    }
}

public sealed record ConceptWithMastery(Guid ConceptId, int MasteryScore);
