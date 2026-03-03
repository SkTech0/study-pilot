using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Infrastructure.Persistence;
using StudyPilot.Infrastructure.Persistence.Repositories;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class DbBackedQuizQuestionGenerationJobQueue : IQuizQuestionGenerationJobQueue
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DbBackedQuizQuestionGenerationJobQueue>? _logger;

    public DbBackedQuizQuestionGenerationJobQueue(IServiceProvider services, ILogger<DbBackedQuizQuestionGenerationJobQueue>? logger = null)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<Guid> EnqueueAsync(Guid quizId, int questionIndex, string? correlationId, CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IQuizQuestionGenerationJobRepository>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<QuizQuestionGenerationJobOptions>>().Value;
        var job = new QuizQuestionGenerationJob
        {
            Id = Guid.NewGuid(),
            QuizId = quizId,
            QuestionIndex = questionIndex,
            CorrelationId = correlationId,
            Status = "Pending",
            RetryCount = 0,
            MaxRetries = Math.Max(1, options.MaxRetries),
            CreatedAtUtc = DateTime.UtcNow
        };
        var jobId = await repo.AddAsync(job, cancellationToken);
        _logger?.LogInformation("QuizQuestionGenerationJobEnqueued JobId={JobId} QuizId={QuizId} QuestionIndex={QuestionIndex} CorrelationId={CorrelationId}",
            jobId, quizId, questionIndex, correlationId);
        return jobId;
    }
}

public sealed class QuizQuestionGenerationJobOptions
{
    public const string SectionName = "QuizQuestionGeneration";
    public string WorkerId { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 3;
    public int ProcessingTimeoutMinutes { get; set; } = 5;
    public int MaxRetries { get; set; } = 1;
}
