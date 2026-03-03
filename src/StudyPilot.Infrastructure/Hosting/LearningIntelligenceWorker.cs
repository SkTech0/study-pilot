using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Learning;
using StudyPilot.Application.Abstractions.Persistence;

namespace StudyPilot.Infrastructure.Hosting;

public sealed class LearningIntelligenceWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StaleSessionAge = TimeSpan.FromDays(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<LearningIntelligenceWorker> _logger;

    public LearningIntelligenceWorker(IServiceProvider services, ILogger<LearningIntelligenceWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var masteryEngine = scope.ServiceProvider.GetRequiredService<IMasteryEngine>();
                var insightGenerator = scope.ServiceProvider.GetRequiredService<ILearningInsightGenerator>();
                var masteryRepo = scope.ServiceProvider.GetRequiredService<IUserConceptMasteryRepository>();
                var sessionRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>();

                var userIds = await masteryRepo.GetDistinctUserIdsAsync(stoppingToken);
                foreach (var userId in userIds)
                {
                    try
                    {
                        await masteryEngine.ApplyTimeDecayAsync(userId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Time decay failed for user {UserId}", userId);
                    }
                }

                await insightGenerator.GenerateInsightsAsync(stoppingToken);

                var cutoff = DateTime.UtcNow - StaleSessionAge;
                var deleted = await sessionRepo.DeleteStaleSessionsAsync(cutoff, stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Cleaned {Count} stale chat sessions", deleted);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LearningIntelligenceWorker run failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
