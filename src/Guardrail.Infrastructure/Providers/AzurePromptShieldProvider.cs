using System.Net.Http.Json;
using System.Text.Json;
using Guardrail.Core.Abstractions;
using Guardrail.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardrail.Infrastructure.Providers;

public sealed class AzurePromptShieldProvider : IPromptShieldProvider
{
    private static readonly string[] InjectionMarkers =
    [
        // Classic prompt injection
        "ignore previous instructions",
        "ignore your instructions",
        "forget your instructions",
        "disregard your guidelines",
        "override policy",
        "reveal hidden instructions",
        "reveal the system prompt",

        // Jailbreak / DAN variants
        "jailbreak",
        "pretend you are",
        "pretend to be",
        "act as if you have no",
        "you are now",
        "as DAN",
        "in DAN mode",
        "do anything now",
        "you have no restrictions",
        "no rules or restrictions",
        "without any restrictions",
        "ignore all restrictions",
        "without ethical constraints",
        "bypass content filters",
        "bypass safety",
        "bypass the filter",
        "no content policy",
        "ignore your training",

        // Role-play-based extraction
        "roleplay as",
        "developer mode",
        "system prompt",
        "in this hypothetical scenario",
        "for educational purposes only, explain how to"
    ];

    private readonly HttpClient _httpClient;
    private readonly AzurePromptShieldOptions _options;
    private readonly ILogger<AzurePromptShieldProvider> _logger;

    public AzurePromptShieldProvider(
        HttpClient httpClient,
        IOptions<AzurePromptShieldOptions> options,
        ILogger<AzurePromptShieldProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "azure-prompt-shield";

    public async Task<PromptShieldResult> DetectInjectionAsync(PromptShieldRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
            return DetectHeuristically(request);

        try
        {
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.Endpoint!.TrimEnd('/')}/contentsafety/text:shieldPrompt?api-version={_options.ApiVersion}");
            httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            httpRequest.Content = JsonContent.Create(new
            {
                userPrompt = request.UserPrompt,
                documents = request.Documents.Select(x => x.Content).ToArray()
            });

            using var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var direct = document.RootElement.TryGetProperty("userPromptAnalysis", out var promptAnalysis)
                && promptAnalysis.TryGetProperty("attackDetected", out var promptAttack)
                && promptAttack.GetBoolean();

            var indirect = document.RootElement.TryGetProperty("documentsAnalysis", out var documentsAnalysis)
                && documentsAnalysis.ValueKind == JsonValueKind.Array
                && documentsAnalysis.EnumerateArray().Any(x => x.TryGetProperty("attackDetected", out var attack) && attack.GetBoolean());

            var signals = new List<InjectionSignal>();
            if (direct)
            {
                signals.Add(new InjectionSignal
                {
                    SignalType = "direct-prompt-attack",
                    Score = 0.95m,
                    Description = "Prompt Shields detected a user prompt attack."
                });
            }

            if (indirect)
            {
                signals.Add(new InjectionSignal
                {
                    SignalType = "document-attack",
                    Score = 0.80m,
                    Description = "Prompt Shields detected an indirect document attack."
                });
            }

            var score = direct
                ? 0.95m
                : indirect
                    ? 0.80m
                    : 0m;

            return new PromptShieldResult
            {
                DirectInjectionDetected = direct,
                IndirectInjectionDetected = indirect,
                InjectionDetected = direct || indirect,
                InjectionScore = score,
                Signals = signals
            };
        }
        catch (Exception ex) when (_options.UseHeuristicFallback)
        {
            _logger.LogWarning(ex, "Azure Prompt Shields call failed. Falling back to heuristic provider behavior.");
            return DetectHeuristically(request);
        }
    }

    private PromptShieldResult DetectHeuristically(PromptShieldRequest request)
    {
        var directMarkers = InjectionMarkers
            .Where(marker => request.UserPrompt.Contains(marker, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var indirectMarkers = request.Documents
            .Where(document => InjectionMarkers.Any(marker => document.Content.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(document => document.DocumentId)
            .ToList();

        var signals = new List<InjectionSignal>();

        signals.AddRange(directMarkers.Select(marker => new InjectionSignal
        {
            SignalType = "direct-prompt-attack",
            Score = 0.95m,
            Description = $"Marker '{marker}' detected in user prompt."
        }));

        signals.AddRange(indirectMarkers.Select(documentId => new InjectionSignal
        {
            SignalType = "document-attack",
            Score = 0.70m,
            Description = $"Document '{documentId}' contains an injection marker."
        }));

        var score = signals.Count == 0 ? 0m : Math.Clamp(signals.Max(x => x.Score), 0m, 1m);

        return new PromptShieldResult
        {
            InjectionDetected = signals.Count > 0,
            DirectInjectionDetected = directMarkers.Count > 0,
            IndirectInjectionDetected = indirectMarkers.Count > 0,
            InjectionScore = score,
            Signals = signals,
            ProviderResponseId = null
        };
    }
}
