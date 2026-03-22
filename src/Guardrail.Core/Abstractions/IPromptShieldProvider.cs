namespace Guardrail.Core.Abstractions;

/// <summary>
/// Abstraction for pluggable prompt-injection and jailbreak detection providers
/// (e.g., Azure AI Prompt Shield, custom classifiers).
/// </summary>
public interface IPromptShieldProvider
{
    string ProviderName { get; }
    Task<PromptShieldResult> DetectInjectionAsync(PromptShieldRequest request, CancellationToken ct = default);
}

public class PromptShieldRequest
{
    public string UserPrompt { get; set; } = string.Empty;
    public List<DocumentContext> Documents { get; set; } = new();
}

public class DocumentContext
{
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain";
}

public class PromptShieldResult
{
    public bool InjectionDetected { get; set; }
    public bool DirectInjectionDetected { get; set; }
    public bool IndirectInjectionDetected { get; set; }
    public decimal InjectionScore { get; set; }
    public List<InjectionSignal> Signals { get; set; } = new();
    public string? ProviderResponseId { get; set; }
}

public class InjectionSignal
{
    public string SignalType { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
}
