namespace Innovayse.Application.Billing.Commands.DeleteQuote;

using FluentValidation;

/// <summary>Validates <see cref="DeleteQuoteCommand"/> before it reaches the handler.</summary>
public sealed class DeleteQuoteValidator : AbstractValidator<DeleteQuoteCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public DeleteQuoteValidator()
    {
        RuleFor(x => x.QuoteId).GreaterThan(0);
    }
}
