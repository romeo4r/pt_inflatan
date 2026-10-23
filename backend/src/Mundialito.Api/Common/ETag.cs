using Microsoft.Net.Http.Headers;
using Mundialito.Domain.Common;

namespace Mundialito.Api.Common;

/// <summary>
/// Optimistic concurrency over HTTP: the row version travels as a strong ETag on reads and comes
/// back in If-Match on writes. The API only enforces it when the client sends it.
/// </summary>
public static class ETag
{
    private static readonly Error InvalidIfMatch = Error.Validation(
        "Concurrency.InvalidIfMatch",
        "La cabecera If-Match no tiene un formato válido. Use el ETag devuelto por la consulta del recurso.");

    /// <summary>Formats a row version as a quoted strong ETag, e.g. "AAAAAAAAB9E=".</summary>
    public static string Format(byte[] rowVersion) => $"\"{Convert.ToBase64String(rowVersion)}\"";

    /// <summary>
    /// Reads If-Match. Returns the expected row version, or null when the header is absent or is
    /// the wildcard "*". A malformed value yields a 400 problem in <paramref name="problem"/>.
    /// </summary>
    public static bool TryGetExpectedVersion(HttpRequest request, out byte[]? expectedVersion, out IResult? problem)
    {
        expectedVersion = null;
        problem = null;

        var header = request.Headers[HeaderNames.IfMatch].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || header.Trim() == "*")
        {
            return true;
        }

        var raw = header.Trim();
        if (raw.StartsWith("W/", StringComparison.Ordinal))
        {
            raw = raw[2..];
        }

        raw = raw.Trim('"');

        try
        {
            expectedVersion = Convert.FromBase64String(raw);
            return expectedVersion.Length > 0;
        }
        catch (FormatException)
        {
            problem = InvalidIfMatch.ToProblem();
            return false;
        }
    }

    /// <summary>200 OK with the body plus an ETag header derived from <paramref name="rowVersion"/>.</summary>
    public static IResult OkWithETag<TValue>(TValue value, byte[]? rowVersion) =>
        rowVersion is null ? Results.Ok(value) : new OkWithETagResult<TValue>(value, Format(rowVersion));

    private sealed class OkWithETagResult<TValue>(TValue value, string etag) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.ETag = etag;
            return Results.Ok(value).ExecuteAsync(httpContext);
        }
    }
}
