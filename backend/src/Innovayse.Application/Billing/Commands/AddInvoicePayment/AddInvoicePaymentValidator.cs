namespace Innovayse.Application.Billing.Commands.AddInvoicePayment;

using FluentValidation;

/// <summary>Validates <see cref="AddInvoicePaymentCommand"/>.</summary>
public sealed class AddInvoicePaymentValidator : AbstractValidator<AddInvoicePaymentCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public AddInvoicePaymentValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Gateway).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TransactionId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
