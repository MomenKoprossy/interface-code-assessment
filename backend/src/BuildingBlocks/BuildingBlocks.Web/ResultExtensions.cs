using BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web;

/// <summary>
/// Maps a railway-oriented <see cref="Result"/> to an <see cref="IResult"/> with the precise HTTP
/// status for its <see cref="ErrorType"/> (§6). Endpoints just return <c>result.ToHttpResult()</c>.
/// Every failure is an RFC 7807 ProblemDetails; an <see cref="Error.Field"/> is reported under <c>errors</c>.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Maps a non-generic result: success → 200 (empty), failure → status by error type.</summary>
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.Ok() : ToProblem(result.Error!.Value);

    /// <summary>Maps a value result: success → 200 with the value, failure → status by error type.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!.Value);

    /// <summary>Success → 201 Created with a Location built from the value; failure → status by error type.</summary>
    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : ToProblem(result.Error!.Value);

    /// <summary>Success → 204 No Content; failure → status by error type.</summary>
    public static IResult ToNoContentHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error!.Value);

    private static IResult ToProblem(Error error) => error.Type switch
    {
        ErrorType.Validation => Results.ValidationProblem(
            new Dictionary<string, string[]> { [error.Field ?? string.Empty] = [error.Message] },
            detail: error.Message),
        ErrorType.NotFound      => Problem(error, StatusCodes.Status404NotFound),
        ErrorType.Conflict      => Problem(error, StatusCodes.Status409Conflict),
        ErrorType.Forbidden     => Problem(error, StatusCodes.Status403Forbidden),
        ErrorType.Unauthorized  => Problem(error, StatusCodes.Status401Unauthorized),
        ErrorType.Unprocessable => Problem(error, StatusCodes.Status422UnprocessableEntity),
        _                       => Problem(error, StatusCodes.Status400BadRequest),
    };

    private static IResult Problem(Error error, int status) =>
        Results.Problem(
            detail: error.Message,
            statusCode: status,
            extensions: error.Field is null
                ? null
                : new Dictionary<string, object?> { ["errors"] = new Dictionary<string, string[]> { [error.Field] = [error.Message] } });
}
