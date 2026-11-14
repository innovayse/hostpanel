using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientBillableItems;

/// <summary>
/// Validates <see cref="ListClientBillableItemsQuery"/>. The page, page size, search and type are the shared billable item list
/// rules (<see cref="BillableItemListQueryValidator{T}"/>); this adds the rule of the client id.
/// </summary>
public sealed class ListClientBillableItemsValidator : BillableItemListQueryValidator<ListClientBillableItemsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientBillableItemsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ListClientIdInvalid"]);
    }
}
