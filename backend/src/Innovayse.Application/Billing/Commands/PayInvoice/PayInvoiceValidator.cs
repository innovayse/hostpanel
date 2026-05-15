namespace Innovayse.Application.Billing.Commands.PayInvoice;

using FluentValidation;

/// <summary>Validates <see cref="PayInvoiceCommand"/>.</summary>
public sealed class PayInvoiceValidator : AbstractValidator<PayInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public PayInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
