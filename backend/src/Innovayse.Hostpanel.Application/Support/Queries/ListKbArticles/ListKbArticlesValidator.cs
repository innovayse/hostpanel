using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles;

/// <summary>
/// Validates <see cref="ListKbArticlesQuery"/>: the page, page size and search every list shares
/// (<see cref="ListQueryValidator{T}"/>) and a positive category id.
/// </summary>
public sealed class ListKbArticlesValidator : ListQueryValidator<ListKbArticlesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListKbArticlesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CategoryId).MustBePositiveId(L["KbCategoryIdInvalid"]);
    }
}
