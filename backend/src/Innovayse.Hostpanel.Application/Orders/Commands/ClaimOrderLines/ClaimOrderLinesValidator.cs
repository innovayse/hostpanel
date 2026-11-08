using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;

/// <summary>
/// Validates <see cref="ClaimOrderLinesCommand"/>: only the shape of the ids; whether they name an order and one of its
/// lines is the handler's answer.
/// </summary>
public sealed class ClaimOrderLinesValidator : LocalizedValidator<ClaimOrderLinesCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ClaimOrderLinesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).GreaterThan(0).WithMessage(L["OrderItemIdInvalid"]).When(x => x.ItemId is not null);
    }
}
