using StudyPilot.Application.Abstractions.AI;

namespace StudyPilot.Infrastructure.AI;

internal sealed class AiConcurrencyHandler : DelegatingHandler
{
    private readonly IAIExecutionLimiter _limiter;

    public AiConcurrencyHandler(IAIExecutionLimiter limiter)
    {
        _limiter = limiter;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _limiter.WaitForCapacityAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                _limiter.NotifySuccess();
            return response;
        }
        finally
        {
            _limiter.Release();
        }
    }
}
