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

    // Retrieval observability (attach to correlationId in logging)
    public static readonly Histogram<double> EmbeddingDurationMs = Meter.CreateHistogram<double>("embedding_duration_ms");
    public static readonly Histogram<double> VectorSearchMs = Meter.CreateHistogram<double>("vector_search_ms");
    public static readonly Histogram<double> HybridRerankMs = Meter.CreateHistogram<double>("hybrid_rerank_ms");
    public static readonly Counter<long> TokensGenerated = Meter.CreateCounter<long>("tokens_generated");
    public static readonly Counter<long> ProviderUsed = Meter.CreateCounter<long>("provider_used");
    public static readonly Counter<long> FallbackCount = Meter.CreateCounter<long>("fallback_count");

    // Phase 3: AI intelligence
    public static readonly Counter<long> MasteryUpdates = Meter.CreateCounter<long>("mastery_updates");
    public static readonly Counter<long> AdaptiveQuizUsage = Meter.CreateCounter<long>("adaptive_quiz_usage");
    public static readonly Counter<long> PersonalizationBoostApplied = Meter.CreateCounter<long>("personalization_boost_applied");
    public static readonly Counter<long> ExplanationStyleUsed = Meter.CreateCounter<long>("explanation_style_used");
    public static readonly Counter<long> LearningInsightsGenerated = Meter.CreateCounter<long>("learning_insights_generated");

    // Phase 4: Tutor
    public static readonly Counter<long> TutorSessionStarted = Meter.CreateCounter<long>("tutor_session_started");
    public static readonly Counter<long> TutorStepTransition = Meter.CreateCounter<long>("tutor_step_transition");
    public static readonly Counter<long> TutorExerciseGenerated = Meter.CreateCounter<long>("tutor_exercise_generated");
    public static readonly Counter<long> TutorExerciseEvaluated = Meter.CreateCounter<long>("tutor_exercise_evaluated");
    public static readonly Counter<long> GoalCompleted = Meter.CreateCounter<long>("goal_completed");

    private static Func<int>? _queueLengthProvider;

    public static void SetQueueLengthProvider(Func<int> provider) => _queueLengthProvider = provider;

    private static int GetQueueLength() => _queueLengthProvider?.Invoke() ?? 0;
}
