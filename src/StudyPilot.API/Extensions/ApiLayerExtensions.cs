using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using StudyPilot.API.Middleware;
using StudyPilot.Application.Common.Behaviors;
using StudyPilot.Infrastructure.Storage;

namespace StudyPilot.API.Extensions;

public static class ApiLayerExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services)
    {
        var assembly = typeof(StudyPilot.Application.Auth.Register.RegisterCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddAutoMapper(cfg => cfg.AddProfile<ApiMappingProfile>(), assembly);
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IPostConfigureOptions<StorageOptions>, ConfigureStorageOptions>();
        return services;
    }

    public static IApplicationBuilder UseApiMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }
}
