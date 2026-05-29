namespace Innovayse.Application.Billing.Commands.ConvertQuoteToInvoice;

using FluentValidation;

/// <summary>Validates <see cref="ConvertQuoteToInvoiceCommand"/> before it reaches the handler.</summary>
public sealed class ConvertQuoteToInvoiceValidator : AbstractValidator<ConvertQuoteToInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public ConvertQuoteToInvoiceValidator()
    {
        RuleFor(x => x.QuoteId).GreaterThan(0);
    }
}
