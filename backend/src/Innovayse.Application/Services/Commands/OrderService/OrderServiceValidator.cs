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
        // Matched to what the pipeline actually produces, not to the two words the doc comment
        // names. PlaceOrderHandler.ResolvePrice accepts "monthly", "annual" and "annually", in any
        // case, and stores the caller's spelling on the order item; AcceptOrderHandler and
        // FulfillPaidOrderHandler then re-dispatch that stored string as this command. A rule that
        // took only the lower-case first two would have refused a paid order at provisioning time,
        // which FulfillPaidOrderHandler logs Critical and carries on past -- charged, unprovisioned.
        RuleFor(x => x.BillingCycle)
            .NotEmpty()
            .Must(c => c.ToLowerInvariant() is "monthly" or "annual" or "annually")
            .WithMessage("BillingCycle must be 'monthly', 'annual' or 'annually'.");
        RuleFor(x => x.FirstPaymentAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RecurringAmount).GreaterThanOrEqualTo(0);
    }
}
