using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CancelOrder;

/// <summary>
///     Validator for CancelOrderCommand. Checks only the shape of the id; whether it names an
///     order, and whether that order is Pending, are the handler's answers.
/// </summary>
public sealed class CancelOrderValidator : LocalizedValidator<CancelOrderCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CancelOrderValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
    }
}
