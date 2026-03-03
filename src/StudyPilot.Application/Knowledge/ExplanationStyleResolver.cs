using StudyPilot.Application.Knowledge.Models;

namespace StudyPilot.Application.Knowledge;

/// <summary>
/// Select explanation style from average mastery: &lt;40 Beginner, 40-70 Intermediate, &gt;70 Advanced.
/// </summary>
public static class ExplanationStyleResolver
{
    public const int BeginnerThreshold = 40;
    public const int IntermediateThreshold = 70;

    public static ExplanationStyle FromAverageMastery(double averageMastery)
    {
        if (averageMastery < BeginnerThreshold) return ExplanationStyle.Beginner;
        if (averageMastery < IntermediateThreshold) return ExplanationStyle.Intermediate;
        return ExplanationStyle.Advanced;
    }
}
