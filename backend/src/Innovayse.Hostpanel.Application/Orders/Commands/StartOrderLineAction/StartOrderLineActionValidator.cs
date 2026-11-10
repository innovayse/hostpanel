using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.StartOrderLineAction;

/// <summary>
/// Validates <see cref="StartOrderLineActionCommand"/>: only the shape of the ids and the attempt. Whether they name an
/// order, one of its lines and the attempt that holds it is the handler's answer.
/// </summary>
public sealed class StartOrderLineActionValidator : LocalizedValidator<StartOrderLineActionCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public StartOrderLineActionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
        RuleFor(x => x.Attempt).GreaterThan(0).WithMessage(L["OrderLineAttemptInvalid"]);
    }
}
