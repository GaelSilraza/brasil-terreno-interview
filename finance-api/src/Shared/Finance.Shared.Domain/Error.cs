namespace Finance.Shared.Domain;

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public const string ValidationFailedCode = "ValidationFailed";

    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata = new Dictionary<string, object?>();

    public IReadOnlyList<FieldError> FieldErrors { get; init; } = [];

    public IReadOnlyDictionary<string, string[]> ValidationErrors => FieldErrors
        .GroupBy(fieldError => fieldError.Field)
        .ToDictionary(fieldGroup => fieldGroup.Key, fieldGroup => fieldGroup.Select(fieldError => fieldError.Message).Distinct().ToArray());

    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = EmptyMetadata;

    public Error WithMetadata(string key, object? value)
    {
        var metadata = new Dictionary<string, object?>(Metadata)
        {
            [key] = value,
        };

        return this with { Metadata = metadata };
    }

    public static Error Validation(IReadOnlyList<FieldError> fieldErrors)
    {
        return new Error(ValidationFailedCode, "One or more validation errors occurred.", ErrorType.Validation)
        {
            FieldErrors = fieldErrors,
        };
    }
}
