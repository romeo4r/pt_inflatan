using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Mundialito.Api.Telemetry;

/// <summary>
/// Distributed tracing. One trace per HTTP request, with child spans for each command/query
/// (Application), every SQL command (EF Core and Dapper share the SqlClient instrumentation) and
/// outgoing HTTP calls. Exported over OTLP when <c>OpenTelemetry:OtlpEndpoint</c> is configured;
/// otherwise traces still exist in-process (TraceId in headers and logs) but are not shipped.
/// </summary>
public static class OpenTelemetryExtensions
{
    public const string ServiceName = "Mundialito.Api";

    public static IServiceCollection AddTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, serviceVersion: typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(Program.ActivitySource.Name)
                    .AddSource("Mundialito.Application")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Health probes would flood the backend with identical, uninteresting traces.
                        options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options =>
                    {
                        // Parameter values are never captured (SetDbQueryParameters stays false); only the
                        // parameterized statement text, which in this codebase is static SQL.
                        options.RecordException = true;
                    });

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
