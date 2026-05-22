namespace Innovayse.Application.Billing.Commands.CreateQuote;

using FluentValidation;

/// <summary>Validates <see cref="CreateQuoteCommand"/> before it reaches the handler.</summary>
public sealed class CreateQuoteValidator : AbstractValidator<CreateQuoteCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateQuoteValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(500);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.DiscountPercent).InclusiveBetween(0, 100);
        });
    }
}
