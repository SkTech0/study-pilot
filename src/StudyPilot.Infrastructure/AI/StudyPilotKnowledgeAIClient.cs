using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Infrastructure.Services;

namespace StudyPilot.Infrastructure.AI;

public sealed class StudyPilotKnowledgeAIClient : IStudyPilotKnowledgeAIClient
{
    private const string ServiceVersion = "v1";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly AIServiceOptions _options;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public StudyPilotKnowledgeAIClient(HttpClient httpClient, IOptions<AIServiceOptions> options, ICorrelationIdAccessor? correlationIdAccessor = null)
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

    public async Task<EmbeddingsResultDto> CreateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
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
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }

    public async Task<ChatResultDto> ChatAsync(ChatRequestDto request, CancellationToken ct = default)
    {
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
        finally
        {
            sw.Stop();
            StudyPilotMetrics.AIRequestDurationMs.Record(sw.ElapsedMilliseconds);
        }
    }
}

