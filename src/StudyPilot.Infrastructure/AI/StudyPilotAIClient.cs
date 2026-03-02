using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.AI;

public sealed class StudyPilotAIClient : IStudyPilotAIClient
{
    private const string ServiceVersion = "v1";
    private const int HealthDegradedThresholdMs = 3000;

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly AIServiceOptions _options;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public StudyPilotAIClient(HttpClient httpClient, IOptions<AIServiceOptions> options, ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _correlationIdAccessor = correlationIdAccessor;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
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

    public async Task<IReadOnlyList<ConceptDto>> ExtractConceptsAsync(Guid documentId, string text, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new { documentId = documentId.ToString(), text };
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "extract-concepts", content);
            var response = await _httpClient.SendAsync(msg, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ExtractConceptsResponseDto>(_jsonOptions, ct);
            return result?.Concepts ?? (IReadOnlyList<ConceptDto>)Array.Empty<ConceptDto>();
        }
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }

    public async Task<GenerateQuizResultDto> GenerateQuizAsync(Guid documentId, IReadOnlyList<string> concepts, int questionCount, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new { documentId = documentId.ToString(), concepts = concepts.Select(c => new { name = c }).ToList(), questionCount };
            var content = JsonContent.Create(request, options: _jsonOptions);
            using var msg = CreateRequest(HttpMethod.Post, "generate-quiz", content);
            var response = await _httpClient.SendAsync(msg, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GenerateQuizResultDto>(_jsonOptions, ct);
            return result ?? new GenerateQuizResultDto();
        }
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
            StudyPilotMetrics.QuizGenerationDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }

    public async Task<AIHealthStatus> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var msg = CreateRequest(HttpMethod.Get, "health");
            var response = await _httpClient.SendAsync(msg, ct);
            sw.Stop();
            if (!response.IsSuccessStatusCode) return AIHealthStatus.Unhealthy;
            var status = sw.ElapsedMilliseconds >= HealthDegradedThresholdMs ? AIHealthStatus.Degraded : AIHealthStatus.Healthy;
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
            return status;
        }
        catch (OperationCanceledException)
        {
            return AIHealthStatus.Degraded;
        }
        catch (HttpRequestException)
        {
            return AIHealthStatus.Unhealthy;
        }
        catch
        {
            return AIHealthStatus.Unhealthy;
        }
    }

    private sealed class ExtractConceptsResponseDto
    {
        public List<ConceptDto> Concepts { get; set; } = [];
    }
}
