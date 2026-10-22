namespace Finance.Shared.Application.Pagination;

public sealed record CursorPage<TItem>(IReadOnlyList<TItem> Items, string? NextCursor, string? PreviousCursor);
