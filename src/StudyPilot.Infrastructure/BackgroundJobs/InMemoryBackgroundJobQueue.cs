using System.Threading.Channels;
using StudyPilot.Application.Abstractions.BackgroundJobs;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class InMemoryBackgroundJobQueue : IBackgroundJobQueue, IBackgroundQueueMetrics
{
    private readonly Channel<Func<CancellationToken, Task>> _channel = Channel.CreateUnbounded<Func<CancellationToken, Task>>();
    private long _processedCount;
    private int _queuedCount;

    public void Enqueue(Func<CancellationToken, Task> job)
    {
        if (_channel.Writer.TryWrite(job))
            Interlocked.Increment(ref _queuedCount);
    }

    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var job = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _queuedCount);
        return job;
    }

    public int QueuedCount => _queuedCount;

    public long ProcessedCount => _processedCount;

    public void RecordProcessed() => Interlocked.Increment(ref _processedCount);
}
