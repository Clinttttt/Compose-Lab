using ComposeLab.Api.Domain.Common;

namespace ComposeLab.Api.Extensions;

/// <summary>
/// The only place an error category becomes an HTTP status code. Keeping the mapping here is what
/// lets <see cref="Result"/> and <see cref="Error"/> stay free of transport concerns.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error);

    public static IResult ToHttpResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);

    private static IResult ToProblem(Error error) => Results.Problem(
        title: Title(error.Type),
        detail: error.Description,
        statusCode: StatusCode(error.Type),
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private static string Title(ErrorType type) => type switch
    {
        ErrorType.Validation => "The request is not valid.",
        ErrorType.Unauthorized => "Authentication is required.",
        ErrorType.Forbidden => "This action is not allowed.",
        ErrorType.NotFound => "The resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state.",
        _ => "The request could not be completed."
    };
}
