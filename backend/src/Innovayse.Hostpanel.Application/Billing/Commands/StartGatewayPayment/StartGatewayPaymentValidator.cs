using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.StartGatewayPayment;

/// <summary>
///     Validator for <see cref="StartGatewayPaymentCommand"/>. Checks only the shape of the
///     fields; whether the invoice is payable, the module is configured and the return URL is
///     allowed are the handler's answers.
/// </summary>
public sealed class StartGatewayPaymentValidator : LocalizedValidator<StartGatewayPaymentCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public StartGatewayPaymentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0).WithMessage(L["InvoiceIdInvalid"]);
        RuleFor(x => x.Module).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.ReturnUrl).NotEmpty().WithMessage(L["ReturnUrlRequired"])
            .MustBeAbsoluteWebUrl(L["ReturnUrlInvalid"]);

        // The wording version is the evidence of the consent a saved method needs; a payment that saves nothing has none.
        When(
            x => x.SavePaymentMethod,
            () => RuleFor(x => x.ConsentTextVersion)
                .MustBeConsentTextVersion(L["ConsentTextVersionInvalid", CardChargeConsent.MaxTextVersionLength]));
    }
}
