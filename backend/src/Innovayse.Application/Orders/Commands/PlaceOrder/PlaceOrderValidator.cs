namespace Innovayse.Application.Orders.Commands.PlaceOrder;

using FluentValidation;
using Innovayse.Application.Common;

/// <summary>Validates <see cref="PlaceOrderCommand"/> before it reaches the handler.</summary>
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    /// <summary>Length of an ISO 4217 alpha code, the only shape a currency may arrive in.</summary>
    private const int CurrencyCodeLength = 3;

    /// <summary>Initialises all validation rules for placing an order.</summary>
    /// <param name="caller">
    /// Who is checking out. The command no longer names a client, so whether the registration
    /// fields are required is a question about the credential rather than about the message.
    /// </param>
    public PlaceOrderValidator(ICurrentRequestContext caller)
    {
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();

        // Shape only. Whether the code names a currency this panel offers is an I/O question,
        // and no validator here takes a repository; the handler asks it and refuses with a
        // sentence the customer can read.
        RuleFor(x => x.Currency)
            .Length(CurrencyCodeLength)
            .When(x => x.Currency is not null)
            .WithMessage("'Currency' must be a three-letter ISO 4217 code.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            // The three spellings BillingCycleParser accepts. Anything else used to reach the
            // handler, where the parser's ArgumentException surfaced as a 500 instead of a 400.
            item.RuleFor(i => i.BillingCycle)
                .NotEmpty()
                .Must(c => c.ToLowerInvariant() is "monthly" or "annual" or "annually")
                .WithMessage("BillingCycle must be 'monthly', 'annual' or 'annually'.");
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
