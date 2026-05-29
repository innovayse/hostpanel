namespace Innovayse.Application.Orders.Commands.PlaceOrder;

using FluentValidation;

/// <summary>Validates <see cref="PlaceOrderCommand"/> before it reaches the handler.</summary>
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    /// <summary>Initialises all validation rules for placing an order.</summary>
    public PlaceOrderValidator()
    {
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.BillingCycle).NotEmpty();
        });

        When(x => x.ClientId is null, () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("'First Name' is required for guest checkout.");
            RuleFor(x => x.LastName).NotEmpty().WithMessage("'Last Name' is required for guest checkout.");
            RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid 'Email' is required for guest checkout.");
            RuleFor(x => x.Password).NotEmpty().WithMessage("'Password' is required for guest checkout.");
        });

        When(x => x.ClientId is not null, () =>
        {
            RuleFor(x => x.ClientId).GreaterThan(0);
        });
    }
}
