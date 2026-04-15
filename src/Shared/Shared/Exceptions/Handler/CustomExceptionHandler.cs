using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Shared.Resources;

namespace Shared.Exceptions.Handler;

/// <summary>
/// Handles ONLY technical/unexpected exceptions (bugs, infrastructure, etc.).
/// Business errors are handled via the Result Pattern in the endpoints.
/// </summary>
public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger, IStringLocalizer<SharedMessages> localizer)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception,
            "Unhandled exception: {Message}, Time: {Time}",
            exception.Message, DateTime.UtcNow);

        var problemDetails = new ProblemDetails
        {
            Title = localizer["UnexpectedError"],
            Detail = localizer["InternalServerError"].Value,
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };

        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }
}