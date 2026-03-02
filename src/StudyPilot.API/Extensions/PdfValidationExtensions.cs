namespace StudyPilot.API.Extensions;

public static class PdfValidationExtensions
{
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46, 0x2D };

    public static async Task<bool> IsPdfSignatureAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[PdfMagic.Length];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (stream.CanSeek)
            stream.Position = 0;
        return read == PdfMagic.Length && buffer.AsSpan().SequenceEqual(PdfMagic);
    }
}
