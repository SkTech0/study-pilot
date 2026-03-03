using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.AI;
using StudyPilot.Application.Abstractions.BackgroundJobs;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.AI;
using StudyPilot.Infrastructure.Persistence.Repositories;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.BackgroundJobs;

public sealed class QuizQuestionGenerationJobWorker : BackgroundService
{
    private const int PendingCountPollThrottle = 6;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbBackedQuizQuestionGenerationJobQueue _queue;
    private readonly IOptions<QuizQuestionGenerationJobOptions> _options;
    private readonly ILogger<QuizQuestionGenerationJobWorker> _logger;
    private int _pollCount;

    public QuizQuestionGenerationJobWorker(
        IServiceScopeFactory scopeFactory,
        DbBackedQuizQuestionGenerationJobQueue queue,
        IOptions<QuizQuestionGenerationJobOptions> options,
        ILogger<QuizQuestionGenerationJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = string.IsNullOrEmpty(_options.Value.WorkerId) ? Guid.NewGuid().ToString("N")[..16] : _options.Value.WorkerId;
        var processingTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.Value.ProcessingTimeoutMinutes));
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollIntervalSeconds));
        var maxRetries = _options.Value.MaxRetries;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var jobRepo = scope.ServiceProvider.GetRequiredService<IQuizQuestionGenerationJobRepository>();
                var quizRepo = scope.ServiceProvider.GetRequiredService<IQuizRepository>();
                var conceptRepo = scope.ServiceProvider.GetRequiredService<IConceptRepository>();
                var questionConceptLinkRepo = scope.ServiceProvider.GetRequiredService<IQuestionConceptLinkRepository>();
                var quizConceptOrderRepo = scope.ServiceProvider.GetRequiredService<IQuizConceptOrderRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();

                await jobRepo.ReleaseStuckJobsAsync(processingTimeout, stoppingToken);

                var job = await jobRepo.TryClaimNextAsync(workerId, processingTimeout, maxRetries, stoppingToken);
                if (job is null)
                {
                    if (Interlocked.Increment(ref _pollCount) % PendingCountPollThrottle == 0)
                        _queue.SetPendingCountFromDb(await jobRepo.GetPendingCountAsync(stoppingToken));
                    await Task.Delay(pollInterval, stoppingToken);
                    continue;
                }

                _logger.LogInformation("StepComplete JobId={JobId} QuizId={QuizId} QuestionIndex={QuestionIndex} StepName=job_claimed CorrelationId={CorrelationId}",
                    job.Id, job.QuizId, job.QuestionIndex, job.CorrelationId);

                var llmTimeoutSec = _options.Value.LlmTimeoutSeconds > 0 ? _options.Value.LlmTimeoutSeconds : 30;
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(llmTimeoutSec));

                var question = await quizRepo.GetQuestionByQuizAndIndexAsync(job.QuizId, job.QuestionIndex, timeoutCts.Token);
                if (question is null || question.Status != QuestionGenerationStatus.Generating)
                {
                    await jobRepo.MarkCompletedAsync(job.Id, stoppingToken);
                    continue;
                }

                var quiz = await quizRepo.GetByIdAsync(job.QuizId, timeoutCts.Token);
                if (quiz is null || job.QuestionIndex >= quiz.TotalQuestionCount)
                {
                    await jobRepo.MarkFailedAsync(job.Id, "Quiz not found or index out of range.", false, null, stoppingToken);
                    continue;
                }

                Concept? concept = null;
                var orderedIds = await quizConceptOrderRepo.GetConceptIdsForQuizAsync(job.QuizId, timeoutCts.Token);
                if (orderedIds.Count > job.QuestionIndex)
                {
                    var conceptId = orderedIds[job.QuestionIndex];
                    concept = await conceptRepo.GetByIdAsync(conceptId, timeoutCts.Token);
                }
                if (concept is null)
                {
                    var concepts = await conceptRepo.GetByDocumentIdAsync(quiz.DocumentId, timeoutCts.Token);
                    if (job.QuestionIndex >= concepts.Count)
                    {
                        question.MarkFailed("No concept for question index.");
                        await quizRepo.UpdateQuestionAsync(question, stoppingToken);
                        await unitOfWork.SaveChangesAsync(stoppingToken);
                        await jobRepo.MarkFailedAsync(job.Id, "No concept for question index.", false, null, stoppingToken);
                        continue;
                    }
                    concept = concepts[job.QuestionIndex];
                }

                var conceptInfo = new ConceptInfo(concept.Id, concept.Name, concept.Description);
                GeneratedQuestion? generated = null;
                Exception? providerException = null;

                try
                {
                    generated = await aiService.GenerateQuestionAsync(quiz.DocumentId, quiz.CreatedForUserId, conceptInfo, timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    providerException = new OperationCanceledException("LLM call timed out or cancelled.");
                }
                catch (HttpRequestException ex)
                {
                    providerException = ex;
                }

                if (generated is not null && !string.IsNullOrWhiteSpace(generated.CorrectAnswer))
                {
                    question.MarkReady(
                        generated.Text,
                        generated.QuestionType,
                        generated.CorrectAnswer,
                        generated.Options.ToList(),
                        generated.PromptVersion,
                        generated.ModelName);
                    await quizRepo.UpdateQuestionAsync(question, stoppingToken);
                    await questionConceptLinkRepo.AddAsync(question.Id, concept.Id, stoppingToken);
                    await unitOfWork.SaveChangesAsync(stoppingToken);
                    await jobRepo.MarkCompletedAsync(job.Id, stoppingToken);
                    _logger.LogInformation("StepComplete JobId={JobId} QuizId={QuizId} QuestionIndex={QuestionIndex} StepName=persistence_duration_ms CorrelationId={CorrelationId}",
                        job.Id, job.QuizId, job.QuestionIndex, job.CorrelationId);
                    continue;
                }

                var allowRetry = providerException != null && (job.RetryCount + 1 < maxRetries);
                var failureReason = providerException?.Message ?? "Generation returned no valid question (parse or content).";
                var nextRetry = allowRetry ? DateTime.UtcNow.AddSeconds(Math.Pow(2, job.RetryCount) * 2) : (DateTime?)null;
                await jobRepo.MarkFailedAsync(job.Id, failureReason, allowRetry, nextRetry, stoppingToken);
                if (allowRetry)
                    StudyPilotMetrics.JobRetriesTotal.Add(1, new KeyValuePair<string, object?>("queue", "quiz"));

                if (!allowRetry)
                {
                    question.MarkFailed(failureReason);
                    await quizRepo.UpdateQuestionAsync(question, stoppingToken);
                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }

                _logger.LogWarning("StepComplete JobId={JobId} QuizId={QuizId} QuestionIndex={QuestionIndex} StepName=generation_failed RetryCount={RetryCount} AllowRetry={AllowRetry} CorrelationId={CorrelationId}",
                    job.Id, job.QuizId, job.QuestionIndex, job.RetryCount, allowRetry, job.CorrelationId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuizQuestionGenerationJobWorker poll error");
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollIntervalSeconds)), stoppingToken);
            }
        }
    }
}
