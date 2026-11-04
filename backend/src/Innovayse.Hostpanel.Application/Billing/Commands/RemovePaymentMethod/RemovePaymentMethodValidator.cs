using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RemovePaymentMethod;

/// <summary>
///     Validator for <see cref="RemovePaymentMethodCommand"/>. Checks only the shape of the id;
///     ownership is the handler's answer.
/// </summary>
public sealed class RemovePaymentMethodValidator : LocalizedValidator<RemovePaymentMethodCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RemovePaymentMethodValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GatewayModuleId).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.PaymentMethodId).NotEmpty().WithMessage(L["PaymentMethodIdRequired"]);
    }
}
