using Mundialito.Api.Common;

namespace Mundialito.UnitTests.Api;

public class UtcDateTimeTests
{
    [Fact]
    public void Normalize_Utc_ShouldReturnSameInstant()
    {
        var value = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

        UtcDateTime.Normalize(value).ShouldBe(value);
    }

    [Fact]
    public void Normalize_Unspecified_ShouldBeTreatedAsUtc()
    {
        var value = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Unspecified);

        var normalized = UtcDateTime.Normalize(value);

        normalized.Kind.ShouldBe(DateTimeKind.Utc);
        normalized.Hour.ShouldBe(15);
    }

    [Fact]
    public void Normalize_Local_ShouldConvertToUtc()
    {
        var local = DateTime.SpecifyKind(new DateTime(2026, 11, 7, 15, 0, 0), DateTimeKind.Local);

        var normalized = UtcDateTime.Normalize(local);

        normalized.Kind.ShouldBe(DateTimeKind.Utc);
        normalized.ShouldBe(local.ToUniversalTime());
    }

    [Fact]
    public void Normalize_Null_ShouldStayNull()
    {
        UtcDateTime.Normalize((DateTime?)null).ShouldBeNull();
    }
}
