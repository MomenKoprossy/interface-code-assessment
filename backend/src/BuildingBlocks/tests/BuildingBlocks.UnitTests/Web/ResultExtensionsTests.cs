using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.UnitTests.Web;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Unprocessable, 422)]
    [InlineData(ErrorType.Failure, 400)]
    public void Failure_maps_error_type_to_status(ErrorType type, int expected)
    {
        var error = new Error("code", "message", type);

        StatusOf(Result.Failure(error).ToHttpResult()).Should().Be(expected);
        StatusOf(Result.Failure<int>(error).ToHttpResult()).Should().Be(expected);
    }

    [Fact]
    public void Success_maps_to_200()
    {
        StatusOf(Result.Success().ToHttpResult()).Should().Be(200);
        StatusOf(Result.Success(42).ToHttpResult()).Should().Be(200);
    }

    [Fact]
    public void Success_cannot_carry_an_error_and_failure_hides_its_value()
    {
        Result<int> failed = Error.NotFound("nope");

        failed.IsFailure.Should().BeTrue();
        failed.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
    }

    private static int? StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;
}
