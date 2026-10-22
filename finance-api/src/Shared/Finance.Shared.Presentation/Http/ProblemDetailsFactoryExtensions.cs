using Microsoft.AspNetCore.Mvc;

namespace Finance.Shared.Presentation.Http;

public static class ProblemDetailsFactoryExtensions
{
    public const string ProblemJsonContentType = "application/problem+json";

    public static string TypeUri(int statusCode)
    {
        return $"https://httpstatuses.io/{statusCode}";
    }

    public static ObjectResult ToProblemResult(this ProblemDetails problemDetails)
    {
        var objectResult = new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
        };
        objectResult.ContentTypes.Add(ProblemJsonContentType);
        return objectResult;
    }
}
