using System.Net;
using System.Text.Json;

namespace Guardrail.API.Middleware;

/// <summary>
/// Global exception handler middleware that catches unhandled exceptions,
/// logs them, and converts them to structured JSON error responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path}, correlation {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Do not overwrite a response already started (e.g., streaming responses).
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";
        var correlationId = context.TraceIdentifier;

        var (statusCode, message) = exception switch
        {
            FluentValidation.ValidationException ex => (HttpStatusCode.BadRequest, string.Join("; ", ex.Errors.Select(x => x.ErrorMessage))),
            ArgumentNullException ex   => (HttpStatusCode.BadRequest, ex.Message),
            ArgumentException ex       => (HttpStatusCode.BadRequest, ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied."),
            KeyNotFoundException        => (HttpStatusCode.NotFound, "The requested resource was not found."),
            InvalidOperationException ex => (HttpStatusCode.Conflict, ex.Message),
            OperationCanceledException  => (HttpStatusCode.RequestTimeout, "The request was cancelled."),
            _                           => (HttpStatusCode.InternalServerError,
                                            $"An unexpected error occurred. Reference: {correlationId}")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = message,
            correlationId,
            timestamp = DateTimeOffset.UtcNow
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
