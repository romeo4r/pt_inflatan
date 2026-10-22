using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Mundialito.Api.Common;

namespace Mundialito.Api.Endpoints;

/// <summary>
/// <c>/health</c> reports the API and its database. Docker Compose uses it to know when the API
/// is ready; the body stays small and generic so it leaks nothing about the infrastructure.
/// </summary>
public sealed class HealthEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteResponse,
        })
        .WithName("Health")
        .ExcludeFromDescription();
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
            durationMs = (long)report.TotalDuration.TotalMilliseconds,
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
