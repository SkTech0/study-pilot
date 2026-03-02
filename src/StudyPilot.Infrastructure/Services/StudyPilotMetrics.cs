using System.Diagnostics.Metrics;

namespace StudyPilot.Infrastructure.Services;

public static class StudyPilotMetrics
{
    public const string MeterName = "StudyPilot";

    private static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> DocumentsProcessedTotal = Meter.CreateCounter<long>("documents_processed_total");
    public static readonly Histogram<double> AIRequestDurationMs = Meter.CreateHistogram<double>("ai_request_duration_ms");
    public static readonly ObservableGauge<int> BackgroundQueueLength = Meter.CreateObservableGauge("background_queue_length", () => GetQueueLength());
    public static readonly Histogram<double> QuizGenerationDurationMs = Meter.CreateHistogram<double>("quiz_generation_duration_ms");
    public static readonly Counter<long> HttpRequestsTotal = Meter.CreateCounter<long>("http_requests_total");
    public static readonly Histogram<double> HttpRequestDurationMs = Meter.CreateHistogram<double>("http_request_duration_ms");
    public static readonly Counter<long> BackgroundJobsTotal = Meter.CreateCounter<long>("background_jobs_total");
    public static readonly Counter<long> BackgroundJobFailuresTotal = Meter.CreateCounter<long>("background_job_failures_total");

    private static Func<int>? _queueLengthProvider;

    public static void SetQueueLengthProvider(Func<int> provider) => _queueLengthProvider = provider;

    private static int GetQueueLength() => _queueLengthProvider?.Invoke() ?? 0;
}
