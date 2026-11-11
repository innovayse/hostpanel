using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RetryOrderLine;

/// <summary>
/// Validates <see cref="RetryOrderLineCommand"/>: only the shape of the two ids. Whether they name an order and
/// one of its lines, and whether that line may be retried, are the handler's answers.
/// </summary>
public sealed class RetryOrderLineValidator : LocalizedValidator<RetryOrderLineCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RetryOrderLineValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["OrderItemIdInvalid"]);
    }
}
