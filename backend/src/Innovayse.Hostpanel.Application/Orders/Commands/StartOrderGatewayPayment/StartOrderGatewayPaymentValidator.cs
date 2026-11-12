using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.StartOrderGatewayPayment;

/// <summary>
///     Validator for StartOrderGatewayPaymentCommand. Checks only the shape of the fields; whether
///     the order is payable, the module is configured and the return URL is allowed are the
///     handler's answers.
/// </summary>
public sealed class StartOrderGatewayPaymentValidator : LocalizedValidator<StartOrderGatewayPaymentCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public StartOrderGatewayPaymentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.Module).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.ReturnUrl).NotEmpty().WithMessage(L["ReturnUrlRequired"])
            .MustBeAbsoluteWebUrl(L["ReturnUrlInvalid"]);

        // The wording version is the evidence of the consent a saved method needs; a payment that saves nothing has none.
        this.RuleForConsentTextVersionWhenSaving(x => x.SavePaymentMethod, x => x.ConsentTextVersion, L["ConsentTextVersionInvalid"]);
    }
}
