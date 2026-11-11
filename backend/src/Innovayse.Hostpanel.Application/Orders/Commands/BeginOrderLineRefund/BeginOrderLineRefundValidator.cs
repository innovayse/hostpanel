using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Orders;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;

/// <summary>
/// Validates <see cref="BeginOrderLineRefundCommand"/>: only the shape of the ids, the held attempt and the code; whether
/// they name an order and one of its lines is the handler's answer.
/// </summary>
public sealed class BeginOrderLineRefundValidator : LocalizedValidator<BeginOrderLineRefundCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public BeginOrderLineRefundValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
        RuleFor(x => x.HeldAttempt).GreaterThan(0).When(x => x.HeldAttempt is not null).WithMessage(L["OrderLineAttemptInvalid"]);
        RuleFor(x => x.ErrorCode).MaximumLength(OrderItem.FulfilmentErrorCodeMaxLength);
    }
}
