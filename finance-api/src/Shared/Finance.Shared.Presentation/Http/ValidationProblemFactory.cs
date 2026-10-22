using Finance.Shared.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Shared.Presentation.Http;

public static class ValidationProblemFactory
{
    public static ObjectResult Create(IReadOnlyList<FieldError> fieldErrors, string detail)
    {
        return Create(Error.Validation(fieldErrors) with { Message = detail });
    }

    public static ObjectResult Create(Error error)
    {
        var validationProblemDetails = new ValidationProblemDetails(error.ValidationErrors.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            Type = ProblemDetailsFactoryExtensions.TypeUri(StatusCodes.Status400BadRequest),
            Title = error.Code,
            Status = StatusCodes.Status400BadRequest,
            Detail = error.Message,
        };
        validationProblemDetails.Extensions["code"] = error.Code;
        validationProblemDetails.Extensions["fieldErrors"] = error.FieldErrors;
        return validationProblemDetails.ToProblemResult();
    }
}
