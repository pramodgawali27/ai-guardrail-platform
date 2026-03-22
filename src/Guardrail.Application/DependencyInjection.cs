using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;
using Guardrail.Application.Behaviors;

namespace Guardrail.Application;

/// <summary>
/// Extension methods for wiring up the Application layer services
/// into the ASP.NET Core dependency-injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR handlers, FluentValidation validators, and all
    /// application-layer services defined in <see cref="Guardrail.Application"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register all IRequestHandler<,> and notification handlers.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Register all AbstractValidator<T> implementations discovered in this assembly.
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
