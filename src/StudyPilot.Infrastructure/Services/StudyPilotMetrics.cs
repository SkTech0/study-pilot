using System.Diagnostics.Metrics;

namespace StudyPilot.Infrastructure.Services;

public static class StudyPilotMetrics
{
    public const string MeterName = "StudyPilot";

    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> DocumentsProcessedTotal = Meter.CreateCounter<long>("documents_processed_total");
    public static readonly Histogram<double> AIRequestDurationMs = Meter.CreateHistogram<double>("ai_request_duration_ms");

    public static void RecordAIDuration(string operation, double durationMs) =>
        AIRequestDurationMs.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));
    public static readonly ObservableGauge<int> BackgroundQueueLength = Meter.CreateObservableGauge("background_queue_length", () => GetQueueLength());
    public static readonly ObservableGauge<int> QuizQueueLength = Meter.CreateObservableGauge("quiz_queue_length", () => GetQuizQueueLength());
    public static readonly ObservableGauge<int> EmbeddingQueueLength = Meter.CreateObservableGauge("embedding_queue_length", () => GetEmbeddingQueueLength());
    public static readonly Histogram<double> QuizGenerationDurationMs = Meter.CreateHistogram<double>("quiz_generation_duration_ms");
    public static readonly Counter<long> HttpRequestsTotal = Meter.CreateCounter<long>("http_requests_total");
    public static readonly Histogram<double> HttpRequestDurationMs = Meter.CreateHistogram<double>("http_request_duration_ms");
    public static readonly Counter<long> BackgroundJobsTotal = Meter.CreateCounter<long>("background_jobs_total");
    public static readonly Counter<long> BackgroundJobFailuresTotal = Meter.CreateCounter<long>("background_job_failures_total");
    public static readonly Counter<long> JobRetriesTotal = Meter.CreateCounter<long>("job_retries_total");

    // Retrieval observability (attach to correlationId in logging)
    public static readonly Histogram<double> EmbeddingDurationMs = Meter.CreateHistogram<double>("embedding_duration_ms");
    public static readonly Histogram<double> VectorSearchMs = Meter.CreateHistogram<double>("vector_search_ms");
    public static readonly Histogram<double> HybridRerankMs = Meter.CreateHistogram<double>("hybrid_rerank_ms");
    public static readonly Counter<long> TokensGenerated = Meter.CreateCounter<long>("tokens_generated");
    public static readonly Counter<long> ProviderUsed = Meter.CreateCounter<long>("provider_used");
    public static readonly Counter<long> FallbackCount = Meter.CreateCounter<long>("fallback_count");

    // Knowledge pipeline reliability
    public static readonly Counter<long> OutboxDispatchSuccess = Meter.CreateCounter<long>("knowledge_outbox_dispatch_success");
    public static readonly Counter<long> KnowledgeRecoveryActions = Meter.CreateCounter<long>("knowledge_recovery_actions");
    public static readonly Histogram<double> KnowledgePipelineStageDuration = Meter.CreateHistogram<double>("knowledge_pipeline_stage_duration", "ms");
    public static readonly Counter<long> KnowledgeOutboxRetryTotal = Meter.CreateCounter<long>("knowledge_outbox_retry_total");
    public static readonly Counter<long> KnowledgeRecoveryRepairsTotal = Meter.CreateCounter<long>("knowledge_recovery_repairs_total");
    public static readonly Histogram<double> KnowledgeEmbeddingLatency = Meter.CreateHistogram<double>("knowledge_embedding_latency", "ms");
    public static readonly ObservableGauge<int> KnowledgeStaleDocuments = Meter.CreateObservableGauge("knowledge_stale_documents", () => GetStaleDocumentsCount());

    private static Func<int>? _staleDocumentsCountProvider;
    public static void SetStaleDocumentsCountProvider(Func<int> provider) => _staleDocumentsCountProvider = provider;
    private static int GetStaleDocumentsCount() => _staleDocumentsCountProvider?.Invoke() ?? 0;

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
    private static Func<int>? _quizQueueLengthProvider;
    private static Func<int>? _embeddingQueueLengthProvider;

    public static void SetQueueLengthProvider(Func<int> provider) => _queueLengthProvider = provider;
    public static void SetQuizQueueLengthProvider(Func<int> provider) => _quizQueueLengthProvider = provider;
    public static void SetEmbeddingQueueLengthProvider(Func<int> provider) => _embeddingQueueLengthProvider = provider;

    private static int GetQueueLength() => _queueLengthProvider?.Invoke() ?? 0;
    private static int GetQuizQueueLength() => _quizQueueLengthProvider?.Invoke() ?? 0;
    private static int GetEmbeddingQueueLength() => _embeddingQueueLengthProvider?.Invoke() ?? 0;

    // Phase 6: Autonomous optimization observability
    public static readonly Counter<long> OptimizationAdjustmentsTotal = Meter.CreateCounter<long>("optimization_adjustments_total");
    public static readonly Counter<long> OptimizationRollbacksTotal = Meter.CreateCounter<long>("optimization_rollbacks_total");
    public static readonly ObservableGauge<int> OptimizationCurrentVectorTopK = Meter.CreateObservableGauge("optimization_current_vector_topk", () => GetOptimizationVectorTopK());
    public static readonly ObservableGauge<int> OptimizationCurrentChunkSize = Meter.CreateObservableGauge("optimization_current_chunk_size", () => GetOptimizationChunkSize());
    public static readonly ObservableGauge<int> OptimizationCurrentConcurrency = Meter.CreateObservableGauge("optimization_current_concurrency", () => GetOptimizationConcurrency());
    public static readonly ObservableGauge<int> OptimizationFreezeState = Meter.CreateObservableGauge("optimization_freeze_state", () => GetOptimizationFreezeState());

    private static Func<int>? _optimizationVectorTopKProvider;
    private static Func<int>? _optimizationChunkSizeProvider;
    private static Func<int>? _optimizationConcurrencyProvider;
    private static Func<int>? _optimizationFreezeStateProvider;
    public static void SetOptimizationVectorTopKProvider(Func<int> p) => _optimizationVectorTopKProvider = p;
    public static void SetOptimizationChunkSizeProvider(Func<int> p) => _optimizationChunkSizeProvider = p;
    public static void SetOptimizationConcurrencyProvider(Func<int> p) => _optimizationConcurrencyProvider = p;
    public static void SetOptimizationFreezeStateProvider(Func<int> p) => _optimizationFreezeStateProvider = p;
    private static int GetOptimizationVectorTopK() => _optimizationVectorTopKProvider?.Invoke() ?? 24;
    private static int GetOptimizationChunkSize() => _optimizationChunkSizeProvider?.Invoke() ?? 800;
    private static int GetOptimizationConcurrency() => _optimizationConcurrencyProvider?.Invoke() ?? 4;
    private static int GetOptimizationFreezeState() => _optimizationFreezeStateProvider?.Invoke() ?? 0;
}
