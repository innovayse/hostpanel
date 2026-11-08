using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RefreshOrderStatus;

/// <summary>Validates <see cref="RefreshOrderStatusCommand"/>: only the shape of the id; whether it names an order is the handler's answer.</summary>
public sealed class RefreshOrderStatusValidator : LocalizedValidator<RefreshOrderStatusCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RefreshOrderStatusValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
    }
}
