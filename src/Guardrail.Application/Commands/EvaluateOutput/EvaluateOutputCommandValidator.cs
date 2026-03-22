using FluentValidation;

namespace Guardrail.Application.Commands.EvaluateOutput;

/// <summary>
/// FluentValidation rules for <see cref="EvaluateOutputCommand"/>.
/// </summary>
public sealed class EvaluateOutputCommandValidator : AbstractValidator<EvaluateOutputCommand>
{
    public EvaluateOutputCommandValidator()
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

        RuleFor(x => x.ModelOutput)
            .NotEmpty().WithMessage("Model output cannot be empty.");

        RuleFor(x => x.ModelOutput)
            .MaximumLength(500_000).WithMessage("Model output exceeds maximum allowed length of 500,000 characters.");

        RuleFor(x => x.OutputSchemaJson)
            .MaximumLength(50_000).When(x => x.OutputSchemaJson != null)
            .WithMessage("Output schema JSON exceeds maximum allowed length.");
    }
}
