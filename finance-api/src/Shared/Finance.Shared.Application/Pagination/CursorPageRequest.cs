namespace Finance.Shared.Application.Pagination;

public sealed record CursorPageRequest<TKey>(TKey? After, CursorDirection Direction, int Limit)
    where TKey : class
{
    public const int DefaultLimit = 20;

    public const int MaximumLimit = 100;

    public bool HasCursor => After is not null;
}
