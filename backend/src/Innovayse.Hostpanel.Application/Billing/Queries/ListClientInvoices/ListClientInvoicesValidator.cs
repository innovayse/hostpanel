using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientInvoices;

/// <summary>
/// Validates <see cref="ListClientInvoicesQuery"/>. The page, page size, search and status are the shared invoice list rules
/// (<see cref="InvoiceListQueryValidator{T}"/>); this adds the rule of the client id.
/// </summary>
public sealed class ListClientInvoicesValidator : InvoiceListQueryValidator<ListClientInvoicesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ListClientIdInvalid"]);
    }
}
