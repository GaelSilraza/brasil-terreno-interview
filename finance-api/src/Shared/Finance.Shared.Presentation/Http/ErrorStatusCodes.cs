using Finance.Shared.Domain;
using Microsoft.AspNetCore.Http;

namespace Finance.Shared.Presentation.Http;

public static class ErrorStatusCodes
{
    public static int FromErrorType(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
