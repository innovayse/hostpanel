using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles;

/// <summary>Query to retrieve a page of the knowledge base articles, published or not, narrowed by a search and filters, for staff.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only articles whose title or body, in any language, contain it (case-insensitive, a <c>%</c> or <c>_</c>
/// matches itself) are listed. Blank lists all.
/// </param>
/// <param name="CategoryId">Optional category primary key; <see langword="null"/> for every category.</param>
/// <param name="IsPublished">Optional published state: true for the published articles, false for the drafts; <see langword="null"/> for both.</param>
public sealed record ListKbArticlesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? CategoryId = null,
    bool? IsPublished = null) : IListQuery;
