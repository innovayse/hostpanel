namespace Innovayse.Application.Billing.Commands.DeleteInvoice;

using FluentValidation;

/// <summary>Validates <see cref="DeleteInvoiceCommand"/>.</summary>
public sealed class DeleteInvoiceValidator : AbstractValidator<DeleteInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public DeleteInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
    }
}
