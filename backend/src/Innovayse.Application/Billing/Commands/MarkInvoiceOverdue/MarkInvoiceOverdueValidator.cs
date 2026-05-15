namespace Innovayse.Application.Billing.Commands.MarkInvoiceOverdue;

using FluentValidation;

/// <summary>Validates <see cref="MarkInvoiceOverdueCommand"/>.</summary>
public sealed class MarkInvoiceOverdueValidator : AbstractValidator<MarkInvoiceOverdueCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public MarkInvoiceOverdueValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
    }
}
