using System.Diagnostics;
using System.Text.Json.Serialization;
using Mundialito.Api.Common;
using Mundialito.Api.Idempotency;
using Mundialito.Api.Middleware;
using Mundialito.Api.Telemetry;
using Mundialito.Application;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger: captures startup failures before configuration is read.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // preserveStaticLogger: each host gets its own logger instead of reconfiguring the global one.
    // Without it, a second host in the same process (integration tests) fails with "logger is frozen".
    builder.Host.UseSerilog(
        (context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Mundialito.Api"),
        preserveStaticLogger: true);

    var connectionString = builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException(
            "Connection string 'Database' is not configured. Set ConnectionStrings:Database via user secrets or environment variables.");

    builder.Services
        .AddApplication()
        .AddInfrastructure(connectionString)
        .AddEndpoints();

    // Enums travel as their names ("Forward", "Scheduled"), which reads better than integers.
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Gives the application layer the request ids for the event audit trail.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.GetTraceId();
        });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(CorrelationIdMiddleware.CorrelationIdHeader, CorrelationIdMiddleware.TraceIdHeader, "Location");
        }));

    builder.Services.AddOpenApi();
    builder.Services.AddTelemetry(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandler();
    // Routes with no endpoint (404) and other bare status codes also answer with ProblemDetails.
    app.UseStatusCodePages();
    app.UseMiddleware<CorrelationIdMiddleware>();

    // One structured log line per request with method, path, status and elapsed time.
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.GetCorrelationId());
            diagnosticContext.Set("TraceId", httpContext.GetTraceId());
        });

    app.UseCors();

    // After routing (so endpoint metadata is known) and after correlation (so its logs carry the ids).
    app.UseMiddleware<IdempotencyMiddleware>();

    // API docs stay available in every environment: this is an evaluation deliverable, not a
    // public production system, and the Docker image runs as Production.
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Mundialito API"));

    app.MapEndpoints();

    // Wait for the database, optionally migrate, seed when empty; then start serving.
    await app.Services.InitializeDatabaseAsync(
        new DatabaseInitializationOptions(
            ApplyMigrations: builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false),
            Seed: builder.Configuration.GetValue("Database:SeedOnStartup", true)),
        app.Lifetime.ApplicationStopping);

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposes the entry point to integration tests (WebApplicationFactory).</summary>
public partial class Program
{
    private Program()
    {
    }

    /// <summary>Activity source for custom spans; wired to OpenTelemetry in a later phase.</summary>
    public static readonly ActivitySource ActivitySource = new("Mundialito.Api");
}
