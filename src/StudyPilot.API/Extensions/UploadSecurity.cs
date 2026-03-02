namespace StudyPilot.API.Extensions;

public static class UploadSecurity
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct().ToArray();

    public static bool HasDoubleExtension(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(name)) return false;
        var dots = name.Count(c => c == '.');
        return dots >= 2;
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "document.pdf";
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(name)) return "document.pdf";
        foreach (var c in InvalidChars)
            name = name.Replace(c, '_');
        if (name.Length > 200) name = name[..200];
        return string.IsNullOrEmpty(name) ? "document.pdf" : name;
    }

    public static bool ContainsPathTraversal(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        return fileName.Contains("..", StringComparison.Ordinal) || fileName.Contains('/') || fileName.Contains('\\');
    }
}
