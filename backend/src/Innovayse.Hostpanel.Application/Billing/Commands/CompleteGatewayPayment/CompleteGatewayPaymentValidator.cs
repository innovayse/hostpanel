using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;

/// <summary>
///     Validator for <see cref="CompleteGatewayPaymentCommand"/>. Checks only the shape of the id and of the return
///     parameters; whether the invoice carries a session to verify is the handler's answer.
/// </summary>
public sealed class CompleteGatewayPaymentValidator : LocalizedValidator<CompleteGatewayPaymentCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CompleteGatewayPaymentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0).WithMessage(L["InvoiceIdInvalid"]);
        RuleFor(x => x.ReturnParameters).MustBePaymentReturnParameters(
            L["PaymentReturnParametersTooMany"], L["PaymentReturnParameterNameInvalid"], L["PaymentReturnParameterValueTooLong"]);
    }
}
