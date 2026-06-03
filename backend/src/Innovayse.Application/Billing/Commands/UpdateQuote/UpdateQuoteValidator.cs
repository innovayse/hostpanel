namespace Innovayse.Application.Billing.Commands.UpdateQuote;

using FluentValidation;

/// <summary>Validates <see cref="UpdateQuoteCommand"/> before it reaches the handler.</summary>
public sealed class UpdateQuoteValidator : AbstractValidator<UpdateQuoteCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public UpdateQuoteValidator()
    {
        RuleFor(x => x.QuoteId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(500);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.DiscountPercent).InclusiveBetween(0, 100);
        });
    }
}
