namespace OrbitMesh.Core.Storage;

/// <summary>
/// Represents a paged result set.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// The items in the current page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// The total number of items across all pages.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// The current page number (1-based).
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Factory methods for creating PagedResult instances.
/// </summary>
public static class PagedResult
{
    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    public static PagedResult<T> Empty<T>(int page = 1, int pageSize = 20) =>
        new()
        {
            Items = [],
            TotalCount = 0,
            Page = page,
            PageSize = pageSize
        };

    /// <summary>
    /// Creates a paged result from a collection.
    /// </summary>
    public static PagedResult<T> Create<T>(
        IReadOnlyList<T> items,
        int totalCount,
        int page,
        int pageSize) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
}
