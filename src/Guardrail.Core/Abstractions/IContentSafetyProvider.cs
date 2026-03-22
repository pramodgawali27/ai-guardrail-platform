namespace Guardrail.Core.Abstractions;

/// <summary>
/// Abstraction for pluggable content-safety providers (e.g., Azure AI Content Safety, OpenAI Moderation).
/// </summary>
public interface IContentSafetyProvider
{
    string ProviderName { get; }
    Task<ContentSafetyResult> AnalyzeTextAsync(string text, ContentSafetyOptions? options = null, CancellationToken ct = default);
    Task<ContentSafetyResult> AnalyzeDocumentAsync(string content, string contentType, ContentSafetyOptions? options = null, CancellationToken ct = default);
}

public class ContentSafetyOptions
{
    public List<string> Categories { get; set; } = new();
    public string Language { get; set; } = "en";
    public bool IncludeDetails { get; set; } = true;
}

public class ContentSafetyResult
{
    public bool IsSafe { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public List<ContentSafetyFlag> Flags { get; set; } = new();
    public decimal OverallScore { get; set; }
    public string? ProviderResponseId { get; set; }
    public Dictionary<string, object> RawDetails { get; set; } = new();
}

public class ContentSafetyFlag
{
    public string Category { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool Flagged { get; set; }
    public string? Detail { get; set; }
}
