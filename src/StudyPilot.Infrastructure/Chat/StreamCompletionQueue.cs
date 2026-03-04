using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Chat;
using StudyPilot.Application.Chat;

namespace StudyPilot.Infrastructure.Chat;

public sealed class StreamCompletionQueue : IStreamCompletionQueue
{
    private readonly Channel<(StreamCompletionWorkItem Work, IStreamTokenWriter Writer, TaskCompletionSource StreamComplete, TaskCompletionSource<ChatStatus> StatusTcs)> _channel =
        Channel.CreateUnbounded<(StreamCompletionWorkItem, IStreamTokenWriter, TaskCompletionSource, TaskCompletionSource<ChatStatus>)>(new UnboundedChannelOptions { SingleReader = true });

    private readonly ILogger<StreamCompletionQueue>? _logger;

    public StreamCompletionQueue(ILogger<StreamCompletionQueue>? logger = null) => _logger = logger;

    internal ChannelReader<(StreamCompletionWorkItem Work, IStreamTokenWriter Writer, TaskCompletionSource StreamComplete, TaskCompletionSource<ChatStatus> StatusTcs)> Reader => _channel.Reader;

    public Task EnqueueAsync(
        StreamCompletionWorkItem work,
        IStreamTokenWriter writer,
        TaskCompletionSource streamComplete,
        TaskCompletionSource<ChatStatus> statusTcs,
        CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryWrite((work, writer, streamComplete, statusTcs));
        return Task.CompletedTask;
    }
}
