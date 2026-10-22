using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Finance.Shared.Presentation.Http;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string ConflictCode = "Conflict";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: ProblemDetailsFactoryExtensions.ProblemJsonContentType, cancellationToken: cancellationToken);
        return true;
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        return exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            || exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
    }

    private static ProblemDetails Create(int statusCode, string code, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Type = ProblemDetailsFactoryExtensions.TypeUri(statusCode),
            Title = code,
            Status = statusCode,
            Detail = detail,
        };
        problemDetails.Extensions[FrameworkProblemDetails.CodeExtensionName] = code;
        return problemDetails;
    }

    private ProblemDetails CreateProblemDetails(Exception exception)
    {
        if (exception is BadHttpRequestException badHttpRequestException)
        {
            logger.LogWarning(exception, "The server rejected the request.");
            var statusCode = badHttpRequestException.StatusCode;
            return Create(statusCode, FrameworkProblemDetails.CodeFor(statusCode), badHttpRequestException.Message);
        }

        if (IsUniqueViolation(exception))
        {
            logger.LogWarning(exception, "A unique constraint was violated by a concurrent request.");
            return Create(StatusCodes.Status409Conflict, ConflictCode, "The data was changed by a concurrent request.");
        }

        logger.LogError(exception, "Unhandled exception.");
        return Create(StatusCodes.Status500InternalServerError, FrameworkProblemDetails.UnexpectedErrorCode, "An unexpected error occurred.");
    }
}
