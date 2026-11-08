using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Orders;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;

/// <summary>
/// Validates <see cref="RecordOrderLineOutcomeCommand"/>: only the shape of the ids and the outcome. Whether they
/// name an order and one of its lines is the handler's answer.
/// </summary>
public sealed class RecordOrderLineOutcomeValidator : LocalizedValidator<RecordOrderLineOutcomeCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecordOrderLineOutcomeValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
        RuleFor(x => x.Outcome).IsInEnum().WithMessage(L["OrderLineOutcomeInvalid"]);
        RuleFor(x => x.ErrorCode).MaximumLength(OrderItem.FulfilmentErrorCodeMaxLength);
    }
}
