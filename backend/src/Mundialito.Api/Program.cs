using System.Diagnostics;
using System.Text.Json.Serialization;
using Mundialito.Api.Common;
using Mundialito.Api.Middleware;
using Mundialito.Application;
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

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Mundialito.Api"));

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

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("Mundialito API"));
    }

    app.MapEndpoints();

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
