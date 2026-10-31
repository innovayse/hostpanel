using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeRenewalInvoice;

/// <summary>Validates <see cref="ChargeRenewalInvoiceCommand"/> before the handler executes.</summary>
public sealed class ChargeRenewalInvoiceValidator : LocalizedValidator<ChargeRenewalInvoiceCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ChargeRenewalInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
