namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>
/// One page of a list. Values from the client are kept in a safe range instead of being rejected:
/// the page counts from 1, the size is 1–<see cref="MaxPageSize"/>.
/// </summary>
internal static class Paging
{
    public const int MaxPageSize = 100;

    /// <summary>Paging in the database (OFFSET / FETCH) – the query must be ordered first.</summary>
    public static IQueryable<T> TakePage<T>(this IQueryable<T> source, int page, int pageSize)
    {
        var (skip, take) = ToSkipTake(page, pageSize);
        return source.Skip(skip).Take(take);
    }

    /// <summary>Paging of rows that are already in memory.</summary>
    public static IEnumerable<T> TakePage<T>(this IEnumerable<T> source, int page, int pageSize)
    {
        var (skip, take) = ToSkipTake(page, pageSize);
        return source.Skip(skip).Take(take);
    }

    private static (int Skip, int Take) ToSkipTake(int page, int pageSize)
    {
        var take = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (Math.Max(page, 1) - 1) * take;
        return (skip, take);
    }
}
