using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.DeleteOrder;

/// <summary>
///     Validator for DeleteOrderCommand. Checks only the shape of the id; whether it names an
///     order, and whether that order may be deleted, are the handler's answers.
/// </summary>
public sealed class DeleteOrderValidator : LocalizedValidator<DeleteOrderCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteOrderValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
    }
}
