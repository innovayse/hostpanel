using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListBillableItems;

/// <summary>
/// Validates <see cref="ListBillableItemsQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the client and type filters.
/// </summary>
public sealed class ListBillableItemsValidator : ListQueryValidator<ListBillableItemsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListBillableItemsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["BillableItemClientIdInvalid"]);
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["BillableItemTypeInvalid"]);
    }
}
