using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CompleteOrderGatewayPayment;

/// <summary>
///     Validator for CompleteOrderGatewayPaymentCommand. Checks only the shape of the id; whether
///     it names an order and whether the payment token matches are the handler's answers.
/// </summary>
public sealed class CompleteOrderGatewayPaymentValidator : LocalizedValidator<CompleteOrderGatewayPaymentCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CompleteOrderGatewayPaymentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
    }
}
