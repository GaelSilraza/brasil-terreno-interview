using Finance.Shared.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Shared.Presentation.Http;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ErrorResult(Error error)
    {
        var statusCode = ErrorStatusCodes.FromErrorType(error.Type);

        if (error.Metadata.TryGetValue(ErrorMetadataKeys.IdempotentReplayed, out var replayed) && replayed is true)
        {
            MarkAsIdempotentReplay();
        }

        if (error.Type == ErrorType.Validation)
        {
            return ValidationProblemFactory.Create(error);
        }

        var problemDetails = new ProblemDetails
        {
            Type = ProblemDetailsFactoryExtensions.TypeUri(statusCode),
            Title = error.Code,
            Status = statusCode,
            Detail = error.Message,
        };
        problemDetails.Extensions["code"] = error.Code;

        foreach (var metadata in error.Metadata.Where(pair => pair.Key != ErrorMetadataKeys.IdempotentReplayed))
        {
            problemDetails.Extensions[metadata.Key] = metadata.Value;
        }

        return problemDetails.ToProblemResult();
    }

    protected void MarkAsIdempotentReplay()
    {
        Response.Headers[IdempotencyHeaders.IdempotentReplayed] = "true";
    }
}
