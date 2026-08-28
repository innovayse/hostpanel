namespace Innovayse.Application.Orders.Commands.PlaceOrder;

using FluentValidation;
using Innovayse.Application.Common;

/// <summary>Validates <see cref="PlaceOrderCommand"/> before it reaches the handler.</summary>
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    /// <summary>Initialises all validation rules for placing an order.</summary>
    /// <param name="caller">
    /// Who is checking out. The command no longer names a client, so whether the registration
    /// fields are required is a question about the credential rather than about the message.
    /// </param>
    public PlaceOrderValidator(ICurrentRequestContext caller)
    {
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.BillingCycle).NotEmpty();
        });

        // Guest checkout is "no credential", not "no client id in the body". The handler
        // makes the same call, and it has to be the same call: a caller who could decide
        // which branch applied could place an order against an account that is not theirs.
        When(_ => caller.UserId is null, () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("'First Name' is required for guest checkout.");
            RuleFor(x => x.LastName).NotEmpty().WithMessage("'Last Name' is required for guest checkout.");
            RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid 'Email' is required for guest checkout.");
            RuleFor(x => x.Password).NotEmpty().WithMessage("'Password' is required for guest checkout.");
        });
    }
}
