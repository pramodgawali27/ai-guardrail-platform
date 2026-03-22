using Serilog;
using Serilog.Events;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Guardrail.Application;
using Guardrail.API.Middleware;
using Guardrail.API.Authentication;
using Guardrail.Infrastructure.DependencyInjection;
using Guardrail.Infrastructure.Persistence;

// ── Bootstrap logger (pre-host) ───────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/guardrail-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 31)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Guardrail API host");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog host logger ───────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .ReadFrom.Configuration(ctx.Configuration)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                "logs/guardrail-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
    });

    // ── MVC ───────────────────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();

    // ── Swagger / OpenAPI ─────────────────────────────────────────────────────
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "Enterprise AI Guardrail Platform API",
            Version = "v1",
            Description = "Production-grade AI safety, policy enforcement, and monitoring gateway. " +
                          "All requests require a valid JWT Bearer token."
        });

        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. " +
                          "Enter 'Bearer {token}' in the text input below.",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ── Authentication ────────────────────────────────────────────────────────
    var disableAuth = builder.Configuration.GetValue<bool>("Auth:DisableAuth");
    var authenticationBuilder = builder.Services.AddAuthentication("Bearer");
    if (disableAuth)
    {
        authenticationBuilder.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>("Bearer", _ => { });
    }
    else
    {
        authenticationBuilder.AddJwtBearer("Bearer", options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    }

    // ── Authorization ─────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminPolicy", policy =>
            policy.RequireRole("guardrail-admin"));

        options.AddPolicy("ReadPolicy", policy =>
            policy.RequireRole("guardrail-admin", "guardrail-read"));

        options.AddPolicy("EvaluatePolicy", policy =>
            policy.RequireRole("guardrail-admin", "guardrail-evaluator", "guardrail-app"));
    });

    // ── Rate limiting ─────────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        // Per-IP sliding window — prevents any single visitor from hammering the demo
        options.AddSlidingWindowLimiter("ApiLimit", opt =>
        {
            opt.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 30);
            opt.Window = TimeSpan.FromMinutes(1);
            opt.SegmentsPerWindow = 6;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 5;
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.Headers["Retry-After"] = "60";
            await ctx.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded — max 30 requests per minute per IP. Please retry after 60 seconds.",
                correlationId = ctx.HttpContext.TraceIdentifier,
                timestamp = DateTimeOffset.UtcNow
            }, ct);
        };
    });

    // ── OpenTelemetry ─────────────────────────────────────────────────────────
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: builder.Configuration["OpenTelemetry:ServiceName"] ?? "guardrail-api",
            serviceVersion: "1.0.0"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
                opts.EnrichWithHttpRequest = (activity, req) =>
                {
                    activity.SetTag("tenant.id", req.Headers["X-Tenant-Id"].ToString());
                    activity.SetTag("correlation.id", req.Headers["X-Correlation-Id"].ToString());
                };
            })
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<GuardrailDbContext>("database");

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowConfigured", policy =>
        {
            var origins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            if (origins.Length > 0)
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            else
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
    });

    // ── Application layer ─────────────────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Serve wwwroot/index.html as the demo landing page
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("TenantId", httpContext.Request.Headers["X-Tenant-Id"].ToString());
            diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Guardrail API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowConfigured");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers().RequireRateLimiting("ApiLimit");
    app.MapHealthChecks("/api/health");

    app.MapGet("/api/version", () => Results.Ok(new
    {
        Version = "1.0.0",
        Service = "Guardrail API",
        BuildDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
    })).AllowAnonymous();

    Log.Information("Guardrail API host ready — environment: {Environment}", app.Environment.EnvironmentName);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Guardrail API host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
