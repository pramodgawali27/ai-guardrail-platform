using System.Net.Http.Json;
using System.Text.Json;
using Guardrail.Core.Abstractions;
using Guardrail.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardrail.Infrastructure.Providers;

public sealed class AzureContentSafetyProvider : IContentSafetyProvider
{
    private readonly HttpClient _httpClient;
    private readonly AzureContentSafetyOptions _options;
    private readonly ILogger<AzureContentSafetyProvider> _logger;

    public AzureContentSafetyProvider(
        HttpClient httpClient,
        IOptions<AzureContentSafetyOptions> options,
        ILogger<AzureContentSafetyProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "azure-content-safety";

    public async Task<ContentSafetyResult> AnalyzeTextAsync(
        string text,
        ContentSafetyOptions? options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
            return AnalyzeHeuristically(text);

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.Endpoint!.TrimEnd('/')}/contentsafety/text:analyze?api-version={_options.ApiVersion}");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            request.Content = JsonContent.Create(new { text });

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var flags = new List<ContentSafetyFlag>();

            if (document.RootElement.TryGetProperty("categoriesAnalysis", out var categoriesAnalysis) &&
                categoriesAnalysis.ValueKind == JsonValueKind.Array)
            {
                foreach (var category in categoriesAnalysis.EnumerateArray())
                {
                    var categoryName = category.TryGetProperty("category", out var categoryProperty)
                        ? categoryProperty.GetString() ?? "Unknown"
                        : "Unknown";
                    var severity = category.TryGetProperty("severity", out var severityProperty)
                        ? severityProperty.GetInt32()
                        : 0;

                    flags.Add(new ContentSafetyFlag
                    {
                        Category = categoryName,
                        Score = Math.Clamp(severity / 6m, 0m, 1m),
                        Severity = severity.ToString(),
                        Flagged = severity >= 3
                    });
                }
            }

            flags.AddRange(DetectPrivacyFlags(text));
            var overallScore = flags.Count == 0 ? 0m : flags.Max(x => x.Score);

            return new ContentSafetyResult
            {
                IsSafe = overallScore < 0.5m,
                ProviderName = ProviderName,
                Flags = flags,
                OverallScore = overallScore
            };
        }
        catch (Exception ex) when (_options.UseHeuristicFallback)
        {
            _logger.LogWarning(ex, "Azure Content Safety call failed. Falling back to heuristic provider behavior.");
            return AnalyzeHeuristically(text);
        }
    }

    public Task<ContentSafetyResult> AnalyzeDocumentAsync(
        string content,
        string contentType,
        ContentSafetyOptions? options = null,
        CancellationToken ct = default)
        => AnalyzeTextAsync(content, options, ct);

    private ContentSafetyResult AnalyzeHeuristically(string text)
    {
        var flags = new List<ContentSafetyFlag>();

        // Jailbreak / persona injection
        AddIfContains(text, flags, "Jailbreak", 0.92m, "Critical",
            "pretend you are", "pretend to be", "act as if you have no",
            "you are now", "as DAN", "in DAN mode", "do anything now",
            "you have no restrictions", "no rules or restrictions", "without any restrictions",
            "ignore all restrictions", "bypass content filters", "bypass the filter",
            "no content policy", "bypass safety", "ignore your training",
            "roleplay as", "in this hypothetical scenario",
            "for educational purposes only, explain how to");

        AddIfContains(text, flags, "Violence", 0.85m, "High", "kill", "bomb", "weapon", "attack");
        AddIfContains(text, flags, "SelfHarm", 0.90m, "High", "suicide", "self-harm", "hurt myself");
        AddIfContains(text, flags, "Sexual", 0.70m, "Medium", "explicit", "porn", "sexual content");
        AddIfContains(text, flags, "Hate", 0.70m, "Medium", "hate group", "racially inferior", "targeted hate");

        // Destructive operations
        AddIfContains(text, flags, "DestructiveAction", 0.95m, "Critical",
            "drop table", "drop database", "drop all tables",
            "truncate table", "delete all records", "wipe the database",
            "destroy the database", "rm -rf", "format c:", "delete all data");

        // Data exfiltration
        AddIfContains(text, flags, "DataExfiltration", 0.90m, "Critical",
            "email the backup", "send to external", "upload to external",
            "exfiltrate", "send the data to", "export to external",
            "email to external", "ftp to external", "copy to external server");

        flags.AddRange(DetectPrivacyFlags(text));

        var overallScore = flags.Count == 0 ? 0m : flags.Max(x => x.Score);

        return new ContentSafetyResult
        {
            IsSafe = overallScore < 0.5m,
            ProviderName = $"{ProviderName}-heuristic",
            Flags = flags,
            OverallScore = overallScore
        };
    }

    private static IReadOnlyList<ContentSafetyFlag> DetectPrivacyFlags(string text)
    {
        var flags = new List<ContentSafetyFlag>();

        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            flags.Add(new ContentSafetyFlag
            {
                Category = "PII",
                Score = 0.75m,
                Severity = "High",
                Flagged = true,
                Detail = "Email address detected."
            });
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{3}-\d{2}-\d{4}\b"))
        {
            flags.Add(new ContentSafetyFlag
            {
                Category = "PII",
                Score = 0.90m,
                Severity = "Critical",
                Flagged = true,
                Detail = "SSN-like pattern detected."
            });
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(?:MRN|Medical Record Number|Patient ID)\s*[:#]?\s*[A-Z0-9-]{4,}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            flags.Add(new ContentSafetyFlag
            {
                Category = "PHI",
                Score = 0.90m,
                Severity = "Critical",
                Flagged = true,
                Detail = "Medical record identifier detected."
            });
        }

        return flags;
    }

    private static void AddIfContains(
        string text,
        ICollection<ContentSafetyFlag> flags,
        string category,
        decimal score,
        string severity,
        params string[] markers)
    {
        // Use word-boundary matching to avoid false positives like "skills" containing "kill"
        if (markers.Any(marker =>
            System.Text.RegularExpressions.Regex.IsMatch(
                text, $@"\b{System.Text.RegularExpressions.Regex.Escape(marker)}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
        {
            flags.Add(new ContentSafetyFlag
            {
                Category = category,
                Score = score,
                Severity = severity,
                Flagged = true
            });
        }
    }
}
