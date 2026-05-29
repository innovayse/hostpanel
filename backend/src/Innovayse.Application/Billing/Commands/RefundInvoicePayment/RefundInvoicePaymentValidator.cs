namespace Innovayse.Application.Billing.Commands.RefundInvoicePayment;

using FluentValidation;

/// <summary>Validates <see cref="RefundInvoicePaymentCommand"/>.</summary>
public sealed class RefundInvoicePaymentValidator : AbstractValidator<RefundInvoicePaymentCommand>
{
    /// <summary>Valid refund type values.</summary>
    private static readonly string[] ValidRefundTypes = ["Gateway", "Manual", "CreditBalance"];

    /// <summary>Initialises all validation rules.</summary>
    public RefundInvoicePaymentValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Amount must be >= 0 (0 = full refund).");
        RuleFor(x => x.RefundType).Must(t => ValidRefundTypes.Contains(t))
            .WithMessage("RefundType must be Gateway, Manual, or CreditBalance.");
        RuleFor(x => x.Gateway).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RefundTransactionId).NotEmpty()
            .When(x => x.RefundType == "Manual")
            .WithMessage("RefundTransactionId is required for Manual refund type.");
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
