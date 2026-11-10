using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;

/// <summary>
/// Validator for <see cref="ChargeInvoiceStoredMethodCommand"/>. Checks only the shape of the id; whether the invoice
/// may be charged is the handler's answer.
/// </summary>
public sealed class ChargeInvoiceStoredMethodValidator : LocalizedValidator<ChargeInvoiceStoredMethodCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ChargeInvoiceStoredMethodValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
