using Finance.Shared.Domain;
using FluentValidation.Results;

namespace Finance.Shared.Application.Validation;

public static class ValidationResultExtensions
{
    private static readonly IReadOnlyDictionary<string, string> ParameterNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ComparisonValue"] = "comparisonValue",
        ["From"] = "from",
        ["To"] = "to",
        ["MinLength"] = "minLength",
        ["MaxLength"] = "maxLength",
        ["ExpectedPrecision"] = "expectedPrecision",
        ["ExpectedScale"] = "expectedScale",
    };

    private static readonly IReadOnlyDictionary<string, string> ComparisonsByValidatorName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GreaterThanValidator"] = "greaterThan",
        ["GreaterThanOrEqualValidator"] = "greaterThanOrEqual",
        ["LessThanValidator"] = "lessThan",
        ["LessThanOrEqualValidator"] = "lessThanOrEqual",
    };

    public static Error ToError(this ValidationResult validationResult)
    {
        var fieldErrors = validationResult.Errors
            .Select(ToFieldError)
            .ToList();
        return Error.Validation(fieldErrors);
    }

    private static FieldError ToFieldError(ValidationFailure validationFailure)
    {
        var parameters = (validationFailure.FormattedMessagePlaceholderValues ?? new Dictionary<string, object>())
            .Where(placeholder => ParameterNames.ContainsKey(placeholder.Key))
            .ToDictionary(placeholder => ParameterNames[placeholder.Key], placeholder => (object?)placeholder.Value);

        if (validationFailure.ErrorCode is not null && ComparisonsByValidatorName.TryGetValue(validationFailure.ErrorCode, out var comparison))
        {
            parameters["comparison"] = comparison;
        }

        return new FieldError(
            FieldPath.ToCamelCase(validationFailure.PropertyName),
            FieldErrorCodeMapper.Map(validationFailure.ErrorCode),
            validationFailure.ErrorMessage,
            parameters);
    }
}
