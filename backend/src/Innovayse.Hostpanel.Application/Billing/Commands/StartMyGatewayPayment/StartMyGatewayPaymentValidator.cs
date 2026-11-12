using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.StartMyGatewayPayment;

/// <summary>Validates <see cref="StartMyGatewayPaymentCommand"/> before it reaches the handler.</summary>
public sealed class StartMyGatewayPaymentValidator : LocalizedValidator<StartMyGatewayPaymentCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public StartMyGatewayPaymentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.Module).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.ReturnUrl).NotEmpty().WithMessage(L["ReturnUrlRequired"])
            .MustBeAbsoluteWebUrl(L["ReturnUrlInvalid"]);

        // The wording version is the evidence of the consent a saved method needs; a payment that saves nothing has none.
        this.RuleForConsentTextVersionWhenSaving(x => x.SavePaymentMethod, x => x.ConsentTextVersion, L["ConsentTextVersionInvalid"]);
    }
}
