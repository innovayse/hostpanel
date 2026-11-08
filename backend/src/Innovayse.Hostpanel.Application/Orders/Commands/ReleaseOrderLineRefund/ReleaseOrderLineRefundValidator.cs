using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;

/// <summary>
/// Validates <see cref="ReleaseOrderLineRefundCommand"/>: only the shape of the ids and the outcome; whether they name
/// an order and one of its lines is the handler's answer.
/// </summary>
public sealed class ReleaseOrderLineRefundValidator : LocalizedValidator<ReleaseOrderLineRefundCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReleaseOrderLineRefundValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
        RuleFor(x => x.Outcome).IsInEnum().WithMessage(L["OrderLineRefundOutcomeInvalid"]);
    }
}
