using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardrail.Infrastructure.Providers;

public class HuggingFaceOptions
{
    public string Token { get; set; } = string.Empty;
    public string ModelId { get; set; } = "Qwen/Qwen2.5-7B-Instruct-Turbo";
    public int MaxTokens { get; set; } = 512;
    public double Temperature { get; set; } = 0.7;
}

public class HuggingFaceInferenceClient
{
    private readonly HttpClient _http;
    private readonly HuggingFaceOptions _options;
    private readonly ILogger<HuggingFaceInferenceClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HuggingFaceInferenceClient(
        HttpClient http,
        IOptions<HuggingFaceOptions> options,
        ILogger<HuggingFaceInferenceClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Token);

    public async Task<HuggingFaceChatResponse> ChatAsync(
        string userPrompt,
        string? systemPrompt = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return HuggingFaceChatResponse.NotConfigured();

        var messages = new List<HfMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new HfMessage("system", systemPrompt));
        messages.Add(new HfMessage("user", userPrompt));

        var payload = new
        {
            model = _options.ModelId,
            messages,
            max_tokens = _options.MaxTokens,
            temperature = _options.Temperature
        };

        var url = "https://router.huggingface.co/together/v1/chat/completions";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);

        try
        {
            var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HF Inference API returned {Status} for model {Model}: {Body}", response.StatusCode, _options.ModelId, body);
                return HuggingFaceChatResponse.Error($"Model returned {(int)response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return new HuggingFaceChatResponse
            {
                Success = true,
                Content = content.Trim(),
                ModelId = _options.ModelId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HF Inference API call failed");
            return HuggingFaceChatResponse.Error("Failed to reach the model. Check your HF_TOKEN secret.");
        }
    }
}

public class HfMessage
{
    public HfMessage(string role, string content) { Role = role; Content = content; }
    [JsonPropertyName("role")]   public string Role    { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; }
}

public class HuggingFaceChatResponse
{
    public bool   Success  { get; set; }
    public string Content  { get; set; } = string.Empty;
    public string ModelId  { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public static HuggingFaceChatResponse NotConfigured() => new()
    {
        Success = false,
        ErrorMessage = "HF_TOKEN is not set. Add it as a Secret in your HF Space settings."
    };

    public static HuggingFaceChatResponse Error(string msg) => new()
    {
        Success = false,
        ErrorMessage = msg
    };
}
