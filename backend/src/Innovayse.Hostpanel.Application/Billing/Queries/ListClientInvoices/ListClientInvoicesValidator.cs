using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientInvoices;

/// <summary>Validates <see cref="ListClientInvoicesQuery"/>.</summary>
public sealed class ListClientInvoicesValidator : LocalizedValidator<ListClientInvoicesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["InvoiceClientIdInvalid"]);
    }
}
