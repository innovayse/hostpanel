using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListProducts;

/// <summary>
/// Validates <see cref="ListProductsQuery"/> before it reaches the handler. The page, page size and search are the shared
/// list rules (<see cref="ListQueryValidator{T}"/>); this adds the rules of the group, type and status filters.
/// </summary>
public sealed class ListProductsValidator : ListQueryValidator<ListProductsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListProductsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GroupId).MustBePositiveId(L["ProductGroupIdInvalid"]);

        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["ProductListTypeInvalid"]);
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["ProductListStatusInvalid"]);
    }
}
