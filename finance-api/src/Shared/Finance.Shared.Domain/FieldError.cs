namespace Finance.Shared.Domain;

public sealed record FieldError(string Field, string Code, string Message, IReadOnlyDictionary<string, object?> Params);
