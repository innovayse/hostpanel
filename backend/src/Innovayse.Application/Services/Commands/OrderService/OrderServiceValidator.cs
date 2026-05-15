namespace Innovayse.Application.Services.Commands.OrderService;

using FluentValidation;

/// <summary>Validates <see cref="OrderServiceCommand"/> inputs.</summary>
public sealed class OrderServiceValidator : AbstractValidator<OrderServiceCommand>
{
    /// <summary>Initializes validation rules for service ordering.</summary>
    public OrderServiceValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.BillingCycle).Must(c => c == "monthly" || c == "annual")
            .WithMessage("BillingCycle must be 'monthly' or 'annual'.");
    }
}
