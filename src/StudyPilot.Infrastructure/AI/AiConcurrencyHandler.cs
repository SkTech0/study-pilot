using System.Net;

namespace StudyPilot.Infrastructure.AI;

internal sealed class AiConcurrencyHandler : DelegatingHandler
{
    private readonly SemaphoreSlim _semaphore;

    public AiConcurrencyHandler(SemaphoreSlim aiConcurrencySemaphore)
    {
        _semaphore = aiConcurrencySemaphore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
