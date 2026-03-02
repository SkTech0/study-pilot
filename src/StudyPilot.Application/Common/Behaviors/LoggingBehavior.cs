using StudyPilot.Application.Abstractions.Logging;
using MediatR;

namespace StudyPilot.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestLogger _logger;

    public LoggingBehavior(IRequestLogger logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", name);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", name);
        return response;
    }
}
