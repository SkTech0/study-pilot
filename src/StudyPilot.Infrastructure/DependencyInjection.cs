using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Pgvector.EntityFrameworkCore;
using StudyPilot.Application.Abstractions.Auth;
using StudyPilot.Application.Abstractions.Caching;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.UsageGuard;
using StudyPilot.Infrastructure.Caching;
using StudyPilot.Infrastructure.Hosting;
using StudyPilot.Infrastructure.Services;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Logging;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Infrastructure.Auth;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.BackgroundJobs;
using StudyPilot.Infrastructure.Logging;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.DbContext;
using StudyPilot.Application.Abstractions.Learning;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Abstractions.Tutor;
using StudyPilot.Infrastructure.Observability;
using StudyPilot.Infrastructure.Learning;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Tutor;
using StudyPilot.Infrastructure.Storage;
using StudyPilot.Infrastructure.Knowledge;
using StudyPilot.Infrastructure.Resilience;

namespace StudyPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default") ?? config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=StudyPilot;Username=postgres;Password=postgres";

        services.AddDbContext<StudyPilotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            })
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IConceptRepository, ConceptRepository>();
        services.AddScoped<IDocumentChunkRepository, PgVectorEmbeddingRepository>();
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IChatMessageCitationRepository, ChatMessageCitationRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IQuestionConceptLinkRepository, QuestionConceptLinkRepository>();
        services.AddScoped<IUserAnswerRepository, UserAnswerRepository>();
        services.AddScoped<IUserConceptProgressRepository, UserConceptProgressRepository>();
        services.AddScoped<IUserConceptMasteryRepository, UserConceptMasteryRepository>();
        services.AddScoped<ILearningInsightRepository, LearningInsightRepository>();
        services.AddScoped<IQuizConceptOrderRepository, QuizConceptOrderRepository>();
        services.AddScoped<ITutorSessionRepository, TutorSessionRepository>();
        services.AddScoped<ILearningGoalRepository, LearningGoalRepository>();
        services.AddScoped<ITutorExerciseRepository, TutorExerciseRepository>();
        services.AddScoped<ITutorMessageRepository, TutorMessageRepository>();
        services.AddScoped<IMasteryEngine, MasteryEngine>();
        services.AddScoped<ILearningInsightGenerator, LearningInsightGenerator>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.Configure<UsageGuardOptions>(config.GetSection(UsageGuardOptions.SectionName));
        services.AddScoped<IUsageGuardService, UsageGuardService>();

        services.Configure<AIServiceOptions>(config.GetSection(AIServiceOptions.SectionName));
        services.Configure<ChaosSimulationOptions>(config.GetSection(ChaosSimulationOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIServiceOptions>>().Value;
            return new SemaphoreSlim(Math.Max(1, opts.MaxConcurrentRequests));
        });
        services.AddTransient<AiConcurrencyHandler>();
        services.AddHttpClient<IStudyPilotAIClient, StudyPilotAIClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            var timeoutSeconds = opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 60;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 300));
        })
        .AddHttpMessageHandler<AiConcurrencyHandler>()
        .AddStandardResilienceHandler(options =>
        {
            // Quiz generation can take several minutes; use 5 min so AI has time to respond.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(300);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(300);
            options.CircuitBreaker.ShouldHandle = _ => new ValueTask<bool>(false);
            // Sampling duration must be at least 2× AttemptTimeout for validation (300s × 2 = 600s).
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(600);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 3;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<IStudyPilotKnowledgeAIClient, StudyPilotKnowledgeAIClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            var timeoutSeconds = opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 60;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 120));
        })
        .AddHttpMessageHandler<AiConcurrencyHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            options.CircuitBreaker.ShouldHandle = _ => new ValueTask<bool>(false);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 3;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IAIService, StudyPilotAIServiceAdapter>();
        services.AddScoped<IQuestionGenerationDispatcher, QuestionGenerationDispatcher>();

        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IVectorSearchService, PgVectorSearchService>();
        services.AddScoped<IHybridSearchService, HybridSearchService>();
        services.AddScoped<IQueryEmbeddingCache, QueryEmbeddingCache>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITutorService, TutorService>();

        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileContentReader, LocalFileContentReader>();

        services.Configure<BackgroundJobOptions>(config.GetSection(BackgroundJobOptions.SectionName));
        services.AddScoped<IBackgroundJobRepository, BackgroundJobRepository>();
        services.AddScoped<IRetryFailedDocumentProcessing, RetryFailedDocumentProcessingService>();
        services.AddScoped<IKnowledgeEmbeddingJobRepository, KnowledgeEmbeddingJobRepository>();
        services.AddSingleton<DbBackedBackgroundJobQueue>();
        services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<DbBackedBackgroundJobQueue>());
        services.AddSingleton<IBackgroundQueueMetrics>(sp => sp.GetRequiredService<DbBackedBackgroundJobQueue>());
        services.AddSingleton<IDocumentProcessingJobFactory, DocumentProcessingJobFactory>();
        services.AddHostedService<BackgroundJobWorker>();
        services.Configure<QuizQuestionGenerationJobOptions>(config.GetSection(QuizQuestionGenerationJobOptions.SectionName));
        services.AddScoped<IQuizQuestionGenerationJobRepository, QuizQuestionGenerationJobRepository>();
        services.AddSingleton<DbBackedQuizQuestionGenerationJobQueue>();
        services.AddSingleton<IQuizQuestionGenerationJobQueue>(sp => sp.GetRequiredService<DbBackedQuizQuestionGenerationJobQueue>());
        services.AddHostedService<QuizQuestionGenerationJobWorker>();
        services.AddSingleton<DbBackedKnowledgeEmbeddingJobQueue>();
        services.AddSingleton<IKnowledgeEmbeddingJobQueue>(sp => sp.GetRequiredService<DbBackedKnowledgeEmbeddingJobQueue>());
        services.AddSingleton<IKnowledgeEmbeddingJobFactory, KnowledgeEmbeddingJobFactory>();
        services.AddHostedService<KnowledgeEmbeddingJobWorker>();
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<DatabaseStartupCheck>();
        services.AddHostedService<AIStartupCheck>();
        services.AddSingleton<WorkerHeartbeatStore>();
        services.AddSingleton<IWorkerHeartbeat>(sp => sp.GetRequiredService<WorkerHeartbeatStore>());
        services.AddHostedService<WorkerHeartbeatService>();
        services.AddHostedService<LaunchValidationHostedService>();
        services.AddHostedService<LearningIntelligenceWorker>();

        services.AddSingleton<IRequestLogger, SerilogRequestLogger>();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
