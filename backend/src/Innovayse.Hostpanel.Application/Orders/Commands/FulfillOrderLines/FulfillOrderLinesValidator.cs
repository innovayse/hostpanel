using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;

/// <summary>
/// Validates <see cref="FulfillOrderLinesCommand"/>: the shape of the ids and of each handed-over claim; whether the ids name
/// an order and its lines is the handler's answer.
/// </summary>
public sealed class FulfillOrderLinesValidator : LocalizedValidator<FulfillOrderLinesCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public FulfillOrderLinesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).GreaterThan(0).WithMessage(L["OrderItemIdInvalid"]).When(x => x.ItemId is not null);
        RuleForEach(x => x.Claimed).ChildRules(line =>
        {
            line.RuleFor(c => c.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
            line.RuleFor(c => c.Attempt).GreaterThan(0).WithMessage(L["OrderLineAttemptInvalid"]);
        });
    }
}
