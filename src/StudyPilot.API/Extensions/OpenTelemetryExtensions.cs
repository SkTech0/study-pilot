using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.API.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddStudyPilotOpenTelemetry(this IServiceCollection services, IConfiguration config)
    {
        var otlpEndpoint = config["OpenTelemetry:OtlpEndpoint"];
        var isProduction = config["ASPNETCORE_ENVIRONMENT"] == "Production";
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService("StudyPilot.API");

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("StudyPilot.API"))
            .WithTracing(t =>
            {
                t.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                else if (!isProduction)
                    t.AddConsoleExporter();
            })
            .WithMetrics(m =>
            {
                m.SetResourceBuilder(resourceBuilder)
                    .AddMeter(StudyPilotMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                else if (!isProduction)
                    m.AddConsoleExporter();
                m.AddPrometheusExporter();
            });

        return services;
    }
}
