using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Infrastructure.Resilience;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.AI;

public sealed class StudyPilotKnowledgeAIClient : IStudyPilotKnowledgeAIClient
{
    private const string ServiceVersion = "v1";
    private const string DependencyName = "AIService";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly AIServiceOptions _options;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;
    private readonly ILogger<StudyPilotKnowledgeAIClient> _logger;
    private readonly ChaosSimulationOptions _chaos;

    public StudyPilotKnowledgeAIClient(HttpClient httpClient, IOptions<AIServiceOptions> options, IOptions<ChaosSimulationOptions> chaos, ILogger<StudyPilotKnowledgeAIClient> logger, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _options = options.Value;
        _chaos = chaos.Value;
        _httpClient = httpClient;
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    }

    private async Task ApplyChaosAsync(CancellationToken ct)
    {
        if (_chaos.SimulateAIUnavailable)
            throw new HttpRequestException("Simulated AI unavailable (Resilience:Chaos:SimulateAIUnavailable).");
        if (_chaos.SimulateSlowAI && _chaos.SimulateSlowAIDelayMs > 0)
            await Task.Delay(_chaos.SimulateSlowAIDelayMs, ct).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Service-Version", ServiceVersion);
        var correlationId = _correlationIdAccessor?.Get();
        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (content != null) request.Content = content;
        return request;
    }

    public async Task<EmbeddingsResultDto> CreateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        await ApplyChaosAsync(ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new { texts };
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "embeddings", content);
            var response = await _httpClient.SendAsync(msg, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<EmbeddingsResultDto>(_jsonOptions, ct);
            return result ?? new EmbeddingsResultDto();
        }
        catch (Exception ex)
        {
            FailureLogging.LogDependencyFailure(_logger, DependencyName, nameof(CreateEmbeddingsAsync), sw, ex, _correlationIdAccessor?.Get());
            throw;
        }
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }

    public async Task<ChatResultDto> ChatAsync(ChatRequestDto request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "chat", content);
            var response = await _httpClient.SendAsync(msg, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ChatResultDto>(_jsonOptions, ct);
            return result ?? new ChatResultDto();
        }
        catch (Exception ex)
        {
            FailureLogging.LogDependencyFailure(_logger, DependencyName, nameof(ChatAsync), sw, ex, _correlationIdAccessor?.Get());
            throw;
        }
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }

    public async Task<StreamChatResultDto> StreamChatAsync(
        ChatRequestDto request,
        Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        await ApplyChaosAsync(ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        var result = new StreamChatResultDto();
        try
        {
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "chat/stream", content);
            using var response = await _httpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var buffer = new StringBuilder();
            var tokenCount = 0L;
            while (true)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("token", out var tokenProp))
                    {
                        var token = tokenProp.GetString() ?? "";
                        if (token.Length > 0)
                        {
                            tokenCount++;
                            await onToken(token).ConfigureAwait(false);
                        }
                    }
                    else if (root.TryGetProperty("done", out var doneProp) && doneProp.ValueKind == JsonValueKind.True)
                    {
                        if (root.TryGetProperty("citedChunkIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
                            foreach (var id in ids.EnumerateArray())
                                result.CitedChunkIds.Add(id.GetString() ?? "");
                        if (root.TryGetProperty("model", out var modelProp))
                            result.Model = modelProp.GetString();
                        break;
                    }
                }
                catch (JsonException) { /* skip malformed line */ }
            }
            if (tokenCount > 0)
                StudyPilotMetrics.TokensGenerated.Add(tokenCount);
            return result;
        }
        catch (Exception ex)
        {
            FailureLogging.LogDependencyFailure(_logger, DependencyName, nameof(StreamChatAsync), sw, ex, _correlationIdAccessor?.Get());
            throw;
        }
        finally
        {
            sw.Stop();
        }
    }

    public async Task<TutorResponseDto> TutorRespondAsync(TutorContextDto request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "tutor/respond", content);
            var response = await _httpClient.SendAsync(msg, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TutorResponseDto>(_jsonOptions, ct);
            return result ?? new TutorResponseDto();
        }
        catch (Exception ex)
        {
            FailureLogging.LogDependencyFailure(_logger, DependencyName, nameof(TutorRespondAsync), sw, ex, _correlationIdAccessor?.Get());
            throw;
        }
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }

    public async Task<TutorStreamResultDto> TutorStreamRespondAsync(TutorContextDto request, Func<string, Task> onToken, CancellationToken ct = default)
    {
        await ApplyChaosAsync(ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        var result = new TutorStreamResultDto();
        try
        {
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "tutor/stream", content);
            using var response = await _httpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            while (true)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("token", out var tokenProp))
                    {
                        var token = tokenProp.GetString() ?? "";
                        if (token.Length > 0) await onToken(token).ConfigureAwait(false);
                    }
                    else if (root.TryGetProperty("done", out var doneProp) && doneProp.ValueKind == JsonValueKind.True)
                    {
                        if (root.TryGetProperty("nextStep", out var step)) result.NextStep = step.GetString() ?? "";
                        if (root.TryGetProperty("optionalExercise", out var ex) && ex.ValueKind == JsonValueKind.Object)
                        {
                            result.OptionalExercise = new TutorExerciseDto
                            {
                                Question = ex.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
                                ExpectedAnswer = ex.TryGetProperty("expectedAnswer", out var ea) ? ea.GetString() ?? "" : "",
                                Difficulty = ex.TryGetProperty("difficulty", out var d) ? d.GetString() ?? "medium" : "medium"
                            };
                        }
                        if (root.TryGetProperty("citedChunkIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
                            foreach (var id in ids.EnumerateArray())
                                result.CitedChunkIds.Add(id.GetString() ?? "");
                        break;
                    }
                }
                catch (JsonException) { /* skip */ }
            }
            return result;
        }
        catch (Exception ex)
        {
            FailureLogging.LogDependencyFailure(_logger, DependencyName, nameof(TutorStreamRespondAsync), sw, ex, _correlationIdAccessor?.Get());
            throw;
        }
        finally
        {
            sw.Stop();
        }
    }

    public async Task<ExerciseEvaluationResultDto> EvaluateExerciseAsync(ExerciseEvaluationRequestDto request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "tutor/evaluate-exercise", content);
            var response = await _httpClient.SendAsync(msg, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ExerciseEvaluationResultDto>(_jsonOptions, ct);
            return result ?? new ExerciseEvaluationResultDto();
        }
        catch (Exception ex)
        {
            FailureLogging.LogDependencyFailure(_logger, DependencyName, nameof(EvaluateExerciseAsync), sw, ex, _correlationIdAccessor?.Get());
            throw;
        }
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }
}

