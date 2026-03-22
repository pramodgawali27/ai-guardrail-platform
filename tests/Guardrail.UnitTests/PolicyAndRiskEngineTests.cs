using Guardrail.Core.Abstractions;
using Guardrail.Core.Domain.Entities;
using Guardrail.Core.Domain.Enums;
using Guardrail.Core.Domain.ValueObjects;
using Guardrail.Infrastructure.DependencyInjection;
using Guardrail.Infrastructure.Firewalls;
using Guardrail.Infrastructure.Policies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Guardrail.UnitTests;

public sealed class PolicyAndRiskEngineTests
{
    [Fact]
    public async Task ResolveEffectivePolicyAsync_MergesGlobalAndApplicationProfiles()
    {
        var globalPolicy = PolicyProfile.Create(
            "global",
            PolicyScope.Global,
            DateTimeOffset.UtcNow.AddDays(-1),
            policyJson: """
            {
              "allowToolUse": false,
              "allowedSourceTypes": ["sharepoint"],
              "allowedTools": ["search-documents"]
            }
            """,
            createdBy: "test");

        var applicationPolicy = PolicyProfile.Create(
            "application",
            PolicyScope.Application,
            DateTimeOffset.UtcNow.AddDays(-1),
            tenantId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
            applicationId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001"),
            policyJson: """
            {
              "allowToolUse": true,
              "requireCitations": true,
              "mandatoryDisclaimer": "Verify before release.",
              "allowedTools": ["summarize-text"]
            }
            """,
            createdBy: "test");

        var repository = new FakePolicyRepository(globalPolicy, applicationPolicy);
        var engine = new JsonPolicyEngine(
            repository,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GuardrailPlatformOptions()),
            NullLogger<JsonPolicyEngine>.Instance);

        var effectivePolicy = await engine.ResolveEffectivePolicyAsync(new TenantContext
        {
            TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
            ApplicationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001"),
            UserId = "tester"
        });

        Assert.True(effectivePolicy.Configuration.RequireCitations);
        Assert.True(effectivePolicy.Constraints.RequireDisclaimer);
        Assert.True(effectivePolicy.Constraints.AllowToolUse);
        Assert.Contains("search-documents", effectivePolicy.Configuration.AllowedTools);
        Assert.Contains("summarize-text", effectivePolicy.Configuration.AllowedTools);
        Assert.Contains("sharepoint", effectivePolicy.Configuration.AllowedSourceTypes);
    }

    [Fact]
    public async Task EvaluateRiskAsync_HighPrivacyRisk_BlocksRequest()
    {
        var engine = new WeightedRiskEngine();

        var result = await engine.EvaluateRiskAsync(new RiskEvaluationInput
        {
            TenantContext = new TenantContext
            {
                TenantId = Guid.NewGuid(),
                ApplicationId = Guid.NewGuid(),
                UserId = "tester"
            },
            ContentSafetyResult = new ContentSafetyResult
            {
                IsSafe = false,
                OverallScore = 0.95m,
                Flags =
                [
                    new ContentSafetyFlag
                    {
                        Category = "PII",
                        Score = 0.95m,
                        Severity = "Critical",
                        Flagged = true
                    }
                ]
            },
            Weights = RiskWeights.Default,
            PrivacyRiskThreshold = 0.6m,
            BlockThreshold = 0.85m
        });

        Assert.Equal(DecisionType.Block, result.Decision);
        Assert.True(result.Score.PrivacyRisk >= 0.95m);
        Assert.True(result.Score.NormalizedScore > 0);
    }

    [Fact]
    public async Task ValidateToolsAsync_ApprovalRequiredTool_IsFlagged()
    {
        var firewall = new DefaultToolFirewall();

        var result = await firewall.ValidateToolsAsync(new ToolValidationRequest
        {
            TenantContext = new TenantContext
            {
                TenantId = Guid.NewGuid(),
                ApplicationId = Guid.NewGuid(),
                UserId = "tester"
            },
            RequestedTools =
            [
                new ToolCallDescriptor { ToolName = "export-files" }
            ],
            Policy = new EffectivePolicy
            {
                Configuration = new PolicyConfiguration
                {
                    AllowedTools = ["export-files"],
                    ApprovalRequiredTools = ["export-files"]
                },
                Constraints = new ConstraintSet
                {
                    AllowToolUse = true,
                    AllowedTools = ["export-files"]
                }
            }
        });

        Assert.True(result.AllToolsAllowed);
        Assert.Contains("export-files", result.ApprovalRequiredTools);
        Assert.Equal(0.65m, result.ToolRiskScore);
    }

    [Fact]
    public async Task ValidateContextAsync_CrossTenantSource_IsBlocked()
    {
        var firewall = new DefaultContextFirewall();

        var result = await firewall.ValidateContextAsync(new ContextFirewallRequest
        {
            TenantContext = new TenantContext
            {
                TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
                ApplicationId = Guid.NewGuid(),
                UserId = "tester"
            },
            BoundaryConfig = new DataBoundaryConfig
            {
                CrossTenantAllowed = false,
                MinimumTrustLevel = SourceTrustLevel.Internal
            },
            RequestedSources =
            [
                new SourceDescriptor
                {
                    SourceId = "external-doc",
                    SourceType = "sharepoint",
                    TenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa9999",
                    TrustLevel = SourceTrustLevel.Internal
                }
            ]
        });

        Assert.False(result.AllSourcesAllowed);
        Assert.True(result.CrossTenantAttemptDetected);
        Assert.Contains("external-doc", result.BlockedSources);
    }

    private sealed class FakePolicyRepository : IPolicyRepository
    {
        private readonly List<PolicyProfile> _profiles;

        public FakePolicyRepository(params PolicyProfile[] profiles)
        {
            _profiles = profiles.ToList();
        }

        public Task<PolicyProfile?> GetEffectivePolicyAsync(Guid tenantId, Guid applicationId, string? domain = null, CancellationToken ct = default)
            => Task.FromResult(_profiles.LastOrDefault());

        public Task<PolicyProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_profiles.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<PolicyProfile>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PolicyProfile>>(_profiles.Where(x => x.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<PolicyProfile>> GetApplicablePoliciesAsync(Guid tenantId, Guid applicationId, string? domain = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PolicyProfile>>(
                _profiles
                    .Where(x => x.Scope == PolicyScope.Global || (x.TenantId == tenantId && x.ApplicationId == applicationId))
                    .OrderBy(x => x.Scope)
                    .ToList());

        public Task<PolicyProfile> AddAsync(PolicyProfile profile, CancellationToken ct = default)
            => Task.FromResult(profile);

        public Task UpdateAsync(PolicyProfile profile, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PolicyRule>> GetRulesForProfileAsync(Guid profileId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PolicyRule>>([]);
    }
}
