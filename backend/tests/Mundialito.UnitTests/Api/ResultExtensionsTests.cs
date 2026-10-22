using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Mundialito.Api.Common;
using Mundialito.Domain.Common;

namespace Mundialito.UnitTests.Api;

public class ResultExtensionsTests
{
    [Fact]
    public void ToHttpResult_OnSuccess_ShouldInvokeSuccessBranch()
    {
        var result = Result.Success(42).ToHttpResult(value => Results.Created($"/things/{value}", value));

        var created = result.ShouldBeOfType<Created<int>>();
        created.Location.ShouldBe("/things/42");
        created.Value.ShouldBe(42);
    }

    [Fact]
    public void ToOk_OnSuccess_ShouldReturn200WithValue()
    {
        var result = Result.Success("hola").ToOk();

        result.ShouldBeOfType<Ok<string>>().Value.ShouldBe("hola");
    }

    [Theory]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest)]
    public void ToProblem_ShouldMapErrorTypeToStatusCode(ErrorType type, int expectedStatus)
    {
        var error = new Error("Sample.Code", "Mensaje en español.", type);

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(expectedStatus);
        problem.ProblemDetails.Detail.ShouldBe("Mensaje en español.");
        problem.ProblemDetails.Extensions["code"].ShouldBe("Sample.Code");
        problem.ProblemDetails.Title.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ToProblem_WithValidationError_ShouldReturn400WithFieldErrors()
    {
        var error = new ValidationError(new Dictionary<string, string[]>
        {
            ["name"] = ["El nombre es obligatorio."],
            ["code"] = ["El código es obligatorio.", "El código es inválido."],
        });

        var problem = Result.Failure<int>(error).ToOk().ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var details = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        details.Errors["name"].ShouldBe(["El nombre es obligatorio."]);
        details.Errors["code"].Length.ShouldBe(2);
        details.Extensions["code"].ShouldBe(ValidationError.DefaultCode);
        details.Detail.ShouldBe(ValidationError.DefaultMessage);
    }

    [Fact]
    public void ToHttpResult_OnFailure_ShouldNotInvokeSuccessBranch()
    {
        var invoked = false;

        Result.Failure(Error.NotFound("X.NotFound", "no existe")).ToHttpResult(() =>
        {
            invoked = true;
            return Results.NoContent();
        });

        invoked.ShouldBeFalse();
    }
}
