namespace Innovayse.Application.Billing.Commands.RemoveInvoiceCredit;

using FluentValidation;

/// <summary>Validates <see cref="RemoveInvoiceCreditCommand"/>.</summary>
public sealed class RemoveInvoiceCreditValidator : AbstractValidator<RemoveInvoiceCreditCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public RemoveInvoiceCreditValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
