using Mundialito.Domain.Common;

namespace Mundialito.UnitTests.Domain.Common;

public class ResultTests
{
    private static readonly Error SampleError = Error.Validation("Sample.Code", "Mensaje de ejemplo.");

    [Fact]
    public void Success_ShouldHaveNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_ShouldExposeError()
    {
        var result = Result.Failure(SampleError);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void SuccessWithValue_ShouldExposeValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Value_OnFailure_ShouldThrow()
    {
        var result = Result.Failure<int>(SampleError);

        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldBeSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldBeFailure()
    {
        Result<string> result = SampleError;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void Create_WithNull_ShouldFailWithNullValue()
    {
        var result = Result.Create<string>(null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.NullValue);
    }

    [Fact]
    public void Combine_ShouldReturnFirstFailure()
    {
        var second = Error.Conflict("Second", "segundo");

        var result = Result.Combine(Result.Success(), Result.Failure(second), Result.Failure(SampleError));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(second);
    }

    [Fact]
    public void Combine_AllSuccess_ShouldSucceed()
    {
        Result.Combine(Result.Success(), Result.Success()).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Match_ShouldInvokeCorrectBranch()
    {
        var success = Result.Success(2).Match(v => v * 10, _ => -1);
        var failure = Result.Failure<int>(SampleError).Match(v => v * 10, _ => -1);

        success.ShouldBe(20);
        failure.ShouldBe(-1);
    }

    [Fact]
    public void Error_ShouldUseValueEquality()
    {
        var a = Error.NotFound("X", "mensaje");
        var b = Error.NotFound("X", "mensaje");

        a.ShouldBe(b);
    }
}
