using System.Diagnostics;
using System.Diagnostics.Metrics;
using Guardrail.Core.Domain.Enums;

namespace Guardrail.Infrastructure.Observability;

public sealed class GuardrailMetrics : IDisposable
{
    private readonly Meter _meter = new("Guardrail.Platform", "1.0.0");
    private readonly Counter<long> _requestsEvaluated;
    private readonly Counter<long> _blockedRequests;
    private readonly Counter<long> _escalatedRequests;
    private readonly Counter<long> _redactionsApplied;
    private readonly Histogram<double> _riskScoreHistogram;
    private readonly Histogram<double> _latencyHistogram;

    public GuardrailMetrics()
    {
        _requestsEvaluated = _meter.CreateCounter<long>("guardrail.requests_evaluated");
        _blockedRequests = _meter.CreateCounter<long>("guardrail.requests_blocked");
        _escalatedRequests = _meter.CreateCounter<long>("guardrail.requests_escalated");
        _redactionsApplied = _meter.CreateCounter<long>("guardrail.redactions_applied");
        _riskScoreHistogram = _meter.CreateHistogram<double>("guardrail.risk_score");
        _latencyHistogram = _meter.CreateHistogram<double>("guardrail.latency_ms");
    }

    public void RecordEvaluation(string phase, DecisionType decision, decimal normalizedRiskScore, long durationMs, int redactionCount = 0)
    {
        var tags = new TagList
        {
            { "phase", phase },
            { "decision", decision.ToString() }
        };

        _requestsEvaluated.Add(1, tags);
        _riskScoreHistogram.Record((double)normalizedRiskScore, tags);
        _latencyHistogram.Record(durationMs, tags);

        if (decision == DecisionType.Block)
            _blockedRequests.Add(1, tags);

        if (decision == DecisionType.Escalate)
            _escalatedRequests.Add(1, tags);

        if (redactionCount > 0)
            _redactionsApplied.Add(redactionCount, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
