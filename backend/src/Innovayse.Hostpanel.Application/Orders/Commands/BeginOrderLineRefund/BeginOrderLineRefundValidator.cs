using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;

/// <summary>
/// Validates <see cref="BeginOrderLineRefundCommand"/>: only the shape of the two ids; whether they name an order and
/// one of its lines is the handler's answer.
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
    }
}
