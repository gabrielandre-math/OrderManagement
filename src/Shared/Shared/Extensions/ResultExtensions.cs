using Microsoft.AspNetCore.Http;
using Shared.Contracts.Results;

namespace Shared.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Converts Result<T> to IResult HTTP.
    /// Success → 200 OK with value.
    /// Failure → HTTP status based on ErrorType.
    /// </summary>
    public static IResult ToProblemResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        return result.Error.ToProblemDetails();
    }

    /// <summary>
    /// Converts Result (without value) to HTTP IResult.
    /// Success → 204 No Content.
    /// Failure → HTTP status based on ErrorType.
    /// </summary>
    public static IResult ToProblemResult(this Result result)
    {
        if (result.IsSuccess)
            return Results.NoContent();

        return result.Error.ToProblemDetails();
    }

    /// <summary>
    /// Converts Result<T> to Created (201) on success.
    /// Useful for POST endpoints that create resources.
    /// </summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, string uri)
    {
        if (result.IsSuccess)
            return Results.Created(uri, result.Value);

        return result.Error.ToProblemDetails();
    }

    // Converts Error → ProblemDetails with the correct status code
    private static IResult ToProblemDetails(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation or ErrorType.BadRequest => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            statusCode: statusCode,
            title: error.Code,      // "Product.NotFound" — stable, does not change by language
            detail: error.Message   // "Product not found" — located via i18n
        );
    }
}
