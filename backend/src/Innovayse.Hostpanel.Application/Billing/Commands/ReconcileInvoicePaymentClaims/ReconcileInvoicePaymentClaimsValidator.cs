using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReconcileInvoicePaymentClaims;

/// <summary>Validator for <see cref="ReconcileInvoicePaymentClaimsCommand"/>, which carries nothing to check.</summary>
public sealed class ReconcileInvoicePaymentClaimsValidator : LocalizedValidator<ReconcileInvoicePaymentClaimsCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReconcileInvoicePaymentClaimsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
