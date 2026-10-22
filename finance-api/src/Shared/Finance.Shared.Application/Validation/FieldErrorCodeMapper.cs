using Finance.Shared.Domain;

namespace Finance.Shared.Application.Validation;

public static class FieldErrorCodeMapper
{
    private static readonly IReadOnlyDictionary<string, string> CodesByValidatorName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["NotEmptyValidator"] = FieldErrorCodes.Required,
        ["NotNullValidator"] = FieldErrorCodes.Required,
        ["RegularExpressionValidator"] = FieldErrorCodes.InvalidFormat,
        ["EmailValidator"] = FieldErrorCodes.InvalidFormat,
        ["GreaterThanValidator"] = FieldErrorCodes.OutOfRange,
        ["GreaterThanOrEqualValidator"] = FieldErrorCodes.OutOfRange,
        ["LessThanValidator"] = FieldErrorCodes.OutOfRange,
        ["LessThanOrEqualValidator"] = FieldErrorCodes.OutOfRange,
        ["InclusiveBetweenValidator"] = FieldErrorCodes.OutOfRange,
        ["ExclusiveBetweenValidator"] = FieldErrorCodes.OutOfRange,
        ["MaximumLengthValidator"] = FieldErrorCodes.InvalidLength,
        ["MinimumLengthValidator"] = FieldErrorCodes.InvalidLength,
        ["LengthValidator"] = FieldErrorCodes.InvalidLength,
        ["ExactLengthValidator"] = FieldErrorCodes.InvalidLength,
        ["PrecisionScaleValidator"] = FieldErrorCodes.InvalidPrecision,
        ["ScalePrecisionValidator"] = FieldErrorCodes.InvalidPrecision,
        ["NotEqualValidator"] = FieldErrorCodes.MustBeDifferent,
        ["EnumValidator"] = FieldErrorCodes.InvalidOption,
        ["StringEnumValidator"] = FieldErrorCodes.InvalidOption,
    };

    public static string Map(string? errorCode)
    {
        if (errorCode is not null && FieldErrorCodes.All.Contains(errorCode))
        {
            return errorCode;
        }

        if (errorCode is not null && CodesByValidatorName.TryGetValue(errorCode, out var mappedCode))
        {
            return mappedCode;
        }

        return FieldErrorCodes.InvalidValue;
    }
}
