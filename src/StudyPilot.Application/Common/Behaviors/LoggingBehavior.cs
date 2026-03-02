using MediatR;
using StudyPilot.Application.Abstractions.Logging;
using StudyPilot.Application.Abstractions.Observability;

namespace StudyPilot.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestLogger _logger;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public LoggingBehavior(IRequestLogger logger, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = _correlationIdAccessor?.Get() ?? "";
        _logger.LogInformation("Handling {RequestName} CorrelationId={CorrelationId}", requestName, correlationId);
        var response = await next();
        _logger.LogInformation("Handled {RequestName} CorrelationId={CorrelationId}", requestName, correlationId);
        return response;
    }
}
