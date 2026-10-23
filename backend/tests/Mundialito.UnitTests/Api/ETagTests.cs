using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Mundialito.Api.Common;

namespace Mundialito.UnitTests.Api;

public class ETagTests
{
    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 7, 209];

    [Fact]
    public void Format_ShouldProduceQuotedBase64()
    {
        ETag.Format(Version).ShouldBe("\"AAAAAAAAB9E=\"");
    }

    [Theory]
    [InlineData("\"AAAAAAAAB9E=\"")]
    [InlineData("AAAAAAAAB9E=")]
    [InlineData("W/\"AAAAAAAAB9E=\"")]
    public void TryGetExpectedVersion_ShouldParseQuotedWeakAndBareForms(string header)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfMatch = header;

        ETag.TryGetExpectedVersion(request, out var version, out var problem).ShouldBeTrue();

        problem.ShouldBeNull();
        version.ShouldBe(Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    public void TryGetExpectedVersion_WithoutHeaderOrWildcard_ShouldReturnNullVersion(string? header)
    {
        var request = new DefaultHttpContext().Request;
        if (header is not null)
        {
            request.Headers.IfMatch = header;
        }

        ETag.TryGetExpectedVersion(request, out var version, out var problem).ShouldBeTrue();

        version.ShouldBeNull();
        problem.ShouldBeNull();
    }

    [Fact]
    public void TryGetExpectedVersion_WithGarbage_ShouldReturn400Problem()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfMatch = "\"not-base64!!\"";

        ETag.TryGetExpectedVersion(request, out _, out var problem).ShouldBeFalse();

        var result = problem.ShouldBeOfType<ProblemHttpResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        result.ProblemDetails.Extensions["code"].ShouldBe("Concurrency.InvalidIfMatch");
    }

    [Fact]
    public void OkWithETag_WithoutVersion_ShouldFallBackToPlainOk()
    {
        ETag.OkWithETag("x", null).ShouldBeOfType<Ok<string>>();
    }
}
