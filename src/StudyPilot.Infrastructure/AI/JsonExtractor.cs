using System.Text.Json;

namespace StudyPilot.Infrastructure.AI;

public static class JsonExtractor
{
    public static string ExtractJson(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse)) return "{}";
        var s = rawResponse.Trim();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = s.Split('\n');
            var start = lines[0].Contains("json", StringComparison.OrdinalIgnoreCase) ? 1 : 1;
            var end = lines.Length > 1 && lines[^1].TrimStart().StartsWith("```", StringComparison.Ordinal) ? lines.Length - 1 : lines.Length;
            s = string.Join("\n", lines.Skip(start).Take(end - start));
        }
        s = s.Trim();
        var first = s.IndexOf('{');
        var last = s.LastIndexOf('}');
        if (first >= 0 && last > first)
            return s.Substring(first, last - first + 1);
        return "{}";
    }

    public static T? Deserialize<T>(string rawResponse, JsonSerializerOptions? options = null) where T : class
    {
        var json = ExtractJson(rawResponse);
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch
        {
            return null;
        }
    }
}
