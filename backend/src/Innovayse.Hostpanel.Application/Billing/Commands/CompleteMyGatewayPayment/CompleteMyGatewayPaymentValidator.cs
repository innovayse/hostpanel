using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyGatewayPayment;

/// <summary>Validates <see cref="CompleteMyGatewayPaymentCommand"/> before it reaches the handler.</summary>
public sealed class CompleteMyGatewayPaymentValidator : LocalizedValidator<CompleteMyGatewayPaymentCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CompleteMyGatewayPaymentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
