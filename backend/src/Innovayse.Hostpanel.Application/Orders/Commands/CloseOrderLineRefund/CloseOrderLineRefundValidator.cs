using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CloseOrderLineRefund;

/// <summary>
/// Validates <see cref="CloseOrderLineRefundCommand"/>: only the shape of the ids; whether they name an order and one of
/// its lines is the handler's answer.
/// </summary>
public sealed class CloseOrderLineRefundValidator : LocalizedValidator<CloseOrderLineRefundCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CloseOrderLineRefundValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
    }
}
