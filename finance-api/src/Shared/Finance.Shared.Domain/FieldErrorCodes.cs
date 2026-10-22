namespace Finance.Shared.Domain;

public static class FieldErrorCodes
{
    public const string Required = "Required";

    public const string InvalidFormat = "InvalidFormat";

    public const string OutOfRange = "OutOfRange";

    public const string InvalidLength = "InvalidLength";

    public const string InvalidPrecision = "InvalidPrecision";

    public const string MustBeDifferent = "MustBeDifferent";

    public const string InvalidOption = "InvalidOption";

    public const string MustBeInFuture = "MustBeInFuture";

    public const string InvalidValue = "InvalidValue";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Required,
        InvalidFormat,
        OutOfRange,
        InvalidLength,
        InvalidPrecision,
        MustBeDifferent,
        InvalidOption,
        MustBeInFuture,
        InvalidValue,
    };
}
