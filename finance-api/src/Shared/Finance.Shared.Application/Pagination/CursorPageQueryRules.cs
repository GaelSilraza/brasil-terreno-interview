using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Shared.Application.Pagination;

public static class CursorPageQueryRules
{
    public static IRuleBuilderOptions<TQuery, int?> ValidLimit<TQuery>(this IRuleBuilder<TQuery, int?> ruleBuilder)
    {
        return ruleBuilder.InclusiveBetween(1, CursorPageRequest<object>.MaximumLimit);
    }

    public static IRuleBuilderOptions<TQuery, string?> ValidDirection<TQuery>(this IRuleBuilder<TQuery, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(direction => direction is null || TryParseDirection(direction, out _))
            .WithErrorCode(FieldErrorCodes.InvalidOption)
            .WithMessage("'Direction' must be 'next' or 'previous'.");
    }

    public static IRuleBuilderOptions<TQuery, string?> ValidCursor<TQuery, TKey>(this IRuleBuilder<TQuery, string?> ruleBuilder)
        where TKey : class
    {
        return ruleBuilder
            .Must(cursor => cursor is null || CursorCodec.TryDecode<TKey>(cursor, out _))
            .WithErrorCode(FieldErrorCodes.InvalidValue)
            .WithMessage("'Cursor' is invalid.");
    }

    public static bool TryParseDirection(string direction, out CursorDirection cursorDirection)
    {
        var isNamedDirection = string.Equals(direction, nameof(CursorDirection.Next), StringComparison.OrdinalIgnoreCase)
            || string.Equals(direction, nameof(CursorDirection.Previous), StringComparison.OrdinalIgnoreCase);
        cursorDirection = CursorDirection.Next;
        return isNamedDirection && Enum.TryParse(direction, ignoreCase: true, out cursorDirection);
    }

    public static CursorPageRequest<TKey> ToRequest<TKey>(string? cursor, string? direction, int? limit)
        where TKey : class
    {
        CursorCodec.TryDecode<TKey>(cursor, out var key);
        var cursorDirection = key is not null && direction is not null && TryParseDirection(direction, out var parsedDirection)
            ? parsedDirection
            : CursorDirection.Next;
        return new CursorPageRequest<TKey>(key, cursorDirection, limit ?? CursorPageRequest<TKey>.DefaultLimit);
    }
}
