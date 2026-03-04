using System.Threading.Channels;
using StudyPilot.Application.Abstractions.Chat;

namespace StudyPilot.Application.Chat;

/// <summary>
/// Wraps a channel writer so stream completion queue worker can write tokens without holding request scope.
/// </summary>
public sealed class ChannelWriterStreamTokenWriter : IStreamTokenWriter
{
    private readonly ChannelWriter<string> _writer;

    public ChannelWriterStreamTokenWriter(ChannelWriter<string> writer) => _writer = writer;

    public async Task WriteAsync(string token, CancellationToken cancellationToken = default) =>
        await _writer.WriteAsync(token, cancellationToken).ConfigureAwait(false);

    public void Complete() => _writer.Complete();
}
