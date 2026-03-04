using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyPilot.Application.Abstractions.Chat;
using StudyPilot.Application.Abstractions.Knowledge;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Chat;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Infrastructure.Chat;

namespace StudyPilot.Infrastructure.Chat;

public sealed class StreamCompletionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StreamCompletionQueue _queue;
    private readonly ILogger<StreamCompletionWorker> _logger;

    public StreamCompletionWorker(
        IServiceScopeFactory scopeFactory,
        StreamCompletionQueue queue,
        ILogger<StreamCompletionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (work, writer, streamComplete, statusTcs) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var status = ChatStatus.Ok;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
                var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
                var citationRepository = scope.ServiceProvider.GetRequiredService<IChatMessageCitationRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var sb = new StringBuilder();
                var streamResult = await chatService.StreamChatAsync(work.Request, async token =>
                {
                    sb.Append(token);
                    await writer.WriteAsync(token, stoppingToken).ConfigureAwait(false);
                }, stoppingToken).ConfigureAwait(false);

                writer.Complete();

                if (streamResult.FallbackUsed)
                    status = ChatStatus.Fallback;

                var fullText = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(fullText))
                {
                    fullText = "I'm temporarily unable to get a response. Please try again shortly.";
                    status = ChatStatus.Fallback;
                }

                var assistantMessage = new ChatMessage(work.Request.SessionId, ChatRole.Assistant, fullText);
                await chatMessageRepository.AddAsync(assistantMessage, stoppingToken).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

                var allowedChunkIds = new HashSet<Guid>(work.Retrieved.Select(c => c.ChunkId));
                var cited = (streamResult.CitedChunkIds ?? Array.Empty<Guid>()).Where(allowedChunkIds.Contains).Distinct().ToList();
                if (cited.Count > 0)
                {
                    await citationRepository.AddRangeAsync(assistantMessage.Id, cited, stoppingToken).ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                }

                statusTcs.TrySetResult(status);
                streamComplete.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                writer.Complete();
                statusTcs.TrySetResult(ChatStatus.Error);
                streamComplete.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stream completion failed SessionId={SessionId}", work.Request.SessionId);
                try { await writer.WriteAsync("I'm temporarily unable to reach the AI provider. Please try again shortly.", stoppingToken).ConfigureAwait(false); } catch { /* ignore */ }
                writer.Complete();
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var fallbackMsg = new ChatMessage(work.Request.SessionId, ChatRole.Assistant, "I'm temporarily unable to reach the AI provider. Please try again shortly.");
                    await chatMessageRepository.AddAsync(fallbackMsg, stoppingToken).ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                }
                catch { /* best effort */ }
                statusTcs.TrySetResult(ChatStatus.Error);
                streamComplete.TrySetResult();
            }
        }
    }
}
