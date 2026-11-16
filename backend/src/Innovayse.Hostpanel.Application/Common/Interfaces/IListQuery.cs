namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The shape every searchable, paged list query shares: which page, how many rows and what the person typed into the
/// search box. A list adds its own filters as further members of its query record; the page, the page size and the
/// search text are validated by <see cref="Innovayse.Hostpanel.Application.Common.Validators.ListQueryValidator{T}"/> and applied by
/// <see cref="Innovayse.Hostpanel.Application.Common.Extensions.TextSearchExtensions"/> and <c>QueryablePagingExtensions.Paginate</c>, so no list re-types them.
/// </summary>
public interface IListQuery
{
    /// <summary>Gets the 1-based page number.</summary>
    public int Page { get; }

    /// <summary>Gets the rows per page; a size outside the use case's limit is refused, not clamped.</summary>
    public int PageSize { get; }

    /// <summary>Gets the optional text to look for; blank means no search.</summary>
    public string? Search { get; }
}
