using FluentValidation;

namespace Guardrail.Application.Commands.EvaluateFull;

/// <summary>
/// FluentValidation rules for <see cref="EvaluateFullCommand"/>.
/// </summary>
public sealed class EvaluateFullCommandValidator : AbstractValidator<EvaluateFullCommand>
{
    public EvaluateFullCommandValidator()
    {
        RuleFor(x => x.TenantContext)
            .NotNull().WithMessage("Tenant context is required.");

        RuleFor(x => x.TenantContext.TenantId)
            .NotEmpty().When(x => x.TenantContext != null)
            .WithMessage("TenantId is required.");

        RuleFor(x => x.TenantContext.ApplicationId)
            .NotEmpty().When(x => x.TenantContext != null)
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.TenantContext.UserId)
            .NotEmpty().When(x => x.TenantContext != null)
            .WithMessage("UserId is required.");

        RuleFor(x => x.UserPrompt)
            .NotEmpty().WithMessage("User prompt cannot be empty.");

        RuleFor(x => x.UserPrompt)
            .MaximumLength(100_000).WithMessage("User prompt exceeds maximum allowed length of 100,000 characters.");

        RuleFor(x => x.SystemPrompt)
            .MaximumLength(50_000).When(x => x.SystemPrompt != null)
            .WithMessage("System prompt exceeds maximum allowed length of 50,000 characters.");

        RuleFor(x => x.ModelOutput)
            .NotEmpty().WithMessage("Model output cannot be empty.");

        RuleFor(x => x.ModelOutput)
            .MaximumLength(500_000).WithMessage("Model output exceeds maximum allowed length of 500,000 characters.");

        RuleFor(x => x.RequestedTools.Count)
            .LessThanOrEqualTo(20).WithMessage("A maximum of 20 tools may be requested per evaluation.");

        RuleFor(x => x.DataSources.Count)
            .LessThanOrEqualTo(50).WithMessage("A maximum of 50 data sources may be supplied per evaluation.");
    }
}
