using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListPublishedKbArticles;

/// <summary>
/// Validates <see cref="ListPublishedKbArticlesQuery"/>. <c>Category</c> and <c>Search</c> are free
/// text with no invalid shape; only their lengths and the paging bounds are checked.
/// </summary>
public sealed class ListPublishedKbArticlesValidator : LocalizedValidator<ListPublishedKbArticlesQuery>
{
    /// <summary>Largest page size the public listing may ask for.</summary>
    private const int MaxPageSize = PagingLimits.MaxPageSize;

    /// <summary>Longest search text the public listing accepts; a longer one cannot be something a person typed to find an article.</summary>
    private const int SearchMaxLength = 200;

    /// <summary>Longest a category filter may be: a category name (255) or a slug (150).</summary>
    private const int CategoryFilterMaxLength = 255;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListPublishedKbArticlesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Category).MaximumLength(CategoryFilterMaxLength).WithMessage(L["KbArticleCategoryTooLong", CategoryFilterMaxLength]);
        RuleFor(x => x.Search).MaximumLength(SearchMaxLength).WithMessage(L["KbArticleSearchTooLong", SearchMaxLength]);
        RuleFor(x => x.Page).MustBePage(L["KbArticlePageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["KbArticlePageSizeRange"]);
    }
}
