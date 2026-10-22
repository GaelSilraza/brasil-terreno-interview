using Microsoft.AspNetCore.Http;

namespace Finance.Shared.Presentation.Http;

public static class FrameworkProblemDetails
{
    public const string CodeExtensionName = "code";

    public const string NotFoundCode = "NotFound";

    public const string MethodNotAllowedCode = "MethodNotAllowed";

    public const string UnsupportedMediaTypeCode = "UnsupportedMediaType";

    public const string RequestRejectedCode = "RequestRejected";

    public const string UnexpectedErrorCode = "UnexpectedError";

    public static void AddMissingCode(ProblemDetailsContext context)
    {
        var problemDetails = context.ProblemDetails;

        if (problemDetails.Extensions.ContainsKey(CodeExtensionName))
        {
            return;
        }

        var statusCode = problemDetails.Status ?? context.HttpContext.Response.StatusCode;
        var code = CodeFor(statusCode);
        problemDetails.Type = ProblemDetailsFactoryExtensions.TypeUri(statusCode);
        problemDetails.Title = code;
        problemDetails.Status = statusCode;
        problemDetails.Extensions[CodeExtensionName] = code;
    }

    public static string CodeFor(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status404NotFound => NotFoundCode,
            StatusCodes.Status405MethodNotAllowed => MethodNotAllowedCode,
            StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaTypeCode,
            >= StatusCodes.Status500InternalServerError => UnexpectedErrorCode,
            _ => RequestRejectedCode,
        };
    }
}
