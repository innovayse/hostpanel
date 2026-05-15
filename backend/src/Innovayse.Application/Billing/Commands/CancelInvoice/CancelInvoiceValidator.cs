namespace Innovayse.Application.Billing.Commands.CancelInvoice;

using FluentValidation;

/// <summary>Validates <see cref="CancelInvoiceCommand"/>.</summary>
public sealed class CancelInvoiceValidator : AbstractValidator<CancelInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CancelInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
    }
}
