using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientBillableItems;

/// <summary>
/// Validates <see cref="ListClientBillableItemsQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the client id and the type filter.
/// </summary>
public sealed class ListClientBillableItemsValidator : ListQueryValidator<ListClientBillableItemsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientBillableItemsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["BillableItemClientIdInvalid"]);
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["BillableItemTypeInvalid"]);
    }
}
