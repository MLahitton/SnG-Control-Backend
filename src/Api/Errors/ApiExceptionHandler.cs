using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SnG.Control.Api.Errors;

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var invalidInput = exception is ArgumentException;
        // .NET 10 can suppress middleware diagnostics for exceptions handled by IExceptionHandler.
        if (!invalidInput) logger.LogError(exception, "Unexpected API error while processing {Method} {Path}.",
            httpContext.Request.Method, httpContext.Request.Path);
        var status = invalidInput ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = invalidInput ? "Invalid request data." : "An unexpected error occurred.",
            Detail = invalidInput ? "Check the supplied request values." : null
        }, cancellationToken);
        return true;
    }
}
