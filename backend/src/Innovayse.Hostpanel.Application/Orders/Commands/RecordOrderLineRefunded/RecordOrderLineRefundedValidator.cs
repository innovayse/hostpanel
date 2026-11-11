using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineRefunded;

/// <summary>
/// Validates <see cref="RecordOrderLineRefundedCommand"/>: only the shape of the two ids; whether they name an
/// order and one of its lines is the handler's answer.
/// </summary>
public sealed class RecordOrderLineRefundedValidator : LocalizedValidator<RecordOrderLineRefundedCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecordOrderLineRefundedValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
    }
}
