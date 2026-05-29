namespace Innovayse.Application.Billing.Commands.ApplyInvoiceCredit;

using FluentValidation;

/// <summary>Validates <see cref="ApplyInvoiceCreditCommand"/>.</summary>
public sealed class ApplyInvoiceCreditValidator : AbstractValidator<ApplyInvoiceCreditCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public ApplyInvoiceCreditValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
