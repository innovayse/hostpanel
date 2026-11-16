using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListPublishedKbArticles;

/// <summary>
/// Validates <see cref="ListPublishedKbArticlesQuery"/>: the page, page size and search every list shares
/// (<see cref="ListQueryValidator{T}"/>) and the length of the category filter, which is free text with no invalid shape.
/// </summary>
public sealed class ListPublishedKbArticlesValidator : ListQueryValidator<ListPublishedKbArticlesQuery>
{
    /// <summary>Longest a category filter may be: a category name (255) or a slug (150).</summary>
    private const int CategoryFilterMaxLength = 255;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListPublishedKbArticlesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Category).MaximumLength(CategoryFilterMaxLength).WithMessage(L["KbArticleCategoryTooLong", CategoryFilterMaxLength]);
    }
}
