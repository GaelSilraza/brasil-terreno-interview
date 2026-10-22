namespace Finance.Shared.Application.Pagination;

public static class CursorPageBuilder
{
    public static CursorPage<TItem> Build<TItem, TKey>(
        IReadOnlyList<TItem> fetchedItems,
        CursorPageRequest<TKey> request,
        Func<TItem, TKey> keySelector)
        where TKey : class
    {
        var hasMoreInRequestedDirection = fetchedItems.Count > request.Limit;
        var pageItems = fetchedItems.Take(request.Limit).ToList();

        if (request.Direction == CursorDirection.Previous)
        {
            pageItems.Reverse();
        }

        var hasNext = request.Direction == CursorDirection.Next ? hasMoreInRequestedDirection : request.HasCursor;
        var hasPrevious = request.Direction == CursorDirection.Previous ? hasMoreInRequestedDirection : request.HasCursor;

        var nextCursor = hasNext && pageItems.Count > 0 ? CursorCodec.Encode(keySelector(pageItems[^1])) : null;
        var previousCursor = hasPrevious && pageItems.Count > 0 ? CursorCodec.Encode(keySelector(pageItems[0])) : null;
        return new CursorPage<TItem>(pageItems, nextCursor, previousCursor);
    }
}
