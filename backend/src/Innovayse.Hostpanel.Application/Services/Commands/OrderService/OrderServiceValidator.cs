using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.OrderService;

/// <summary>Validates <see cref="OrderServiceCommand"/> before the handler executes.</summary>
public sealed class OrderServiceValidator : LocalizedValidator<OrderServiceCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public OrderServiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["OrderServiceClientIdInvalid"]);
        RuleFor(x => x.ProductId).MustBePositiveId(L["OrderServiceProductIdInvalid"]);
        RuleFor(x => x.BillingCycle).IsInEnum().WithMessage(L["OrderServiceBillingCycleInvalid"]);
        RuleFor(x => x.OrderItemId).GreaterThan(0).WithMessage(L["OrderItemIdInvalid"]).When(x => x.OrderItemId is not null);
        RuleFor(x => x.FirstPaymentAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage(L["OrderServiceAmountInvalid"])
            .When(x => x.FirstPaymentAmount is not null);
        RuleFor(x => x.RecurringAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage(L["OrderServiceAmountInvalid"])
            .When(x => x.RecurringAmount is not null);
    }
}
