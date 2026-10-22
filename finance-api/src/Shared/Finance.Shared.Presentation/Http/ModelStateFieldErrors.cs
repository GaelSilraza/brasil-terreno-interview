using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Finance.Shared.Presentation.Http;

public static class ModelStateFieldErrors
{
    private const string InvalidValueMessage = "The value is invalid.";

    private const string BodyField = "body";

    public static IReadOnlyList<FieldError> From(ModelStateDictionary modelState)
    {
        var fieldErrors = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(modelError => ToFieldError(entry.Key, modelError)))
            .ToList();

        var hasSpecificFieldErrors = fieldErrors.Any(fieldError => fieldError.Field != BodyField);
        return hasSpecificFieldErrors
            ? fieldErrors.Where(fieldError => fieldError.Field != BodyField).ToList()
            : fieldErrors;
    }

    private static FieldError ToFieldError(string modelStateKey, ModelError modelError)
    {
        var message = string.IsNullOrWhiteSpace(modelError.ErrorMessage) ? InvalidValueMessage : modelError.ErrorMessage;
        var isMissingValue = message.Contains("required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("was not provided", StringComparison.OrdinalIgnoreCase);
        return new FieldError(
            NormalizeKey(modelStateKey),
            isMissingValue ? FieldErrorCodes.Required : FieldErrorCodes.InvalidValue,
            message,
            new Dictionary<string, object?>());
    }

    private static string NormalizeKey(string modelStateKey)
    {
        var withoutJsonRoot = modelStateKey.StartsWith("$.", StringComparison.Ordinal) ? modelStateKey[2..] : modelStateKey;

        if (withoutJsonRoot is "$" or "" or "request")
        {
            return BodyField;
        }

        return FieldPath.ToCamelCase(withoutJsonRoot);
    }
}
