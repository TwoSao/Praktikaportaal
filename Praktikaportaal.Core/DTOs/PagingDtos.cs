using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Core.DTOs;

/// <summary>One page of results plus the total count, for list endpoints.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];

    /// <summary>Total number of items matching the filters (all pages).</summary>
    /// <example>42</example>
    public int TotalCount { get; set; }

    /// <example>1</example>
    public int Page { get; set; }

    /// <example>10</example>
    public int PageSize { get; set; }

    /// <example>5</example>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>Common paging query parameters (?page=1&amp;pageSize=10).</summary>
public class PagingParameters
{
    public const int MaxPageSize = 100;

    /// <summary>1-based page number.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Items per page (1–100).</summary>
    /// <example>10</example>
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 10;

    public int Skip => (Page - 1) * PageSize;
}
