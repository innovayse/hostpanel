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
        // Required only where the handler reads it. RefundInvoicePaymentHandler overwrites
        // Gateway with the literal "Credit Balance" when RefundType is CreditBalance, so demanding
        // a non-empty value for that branch would refuse a request over a field that is discarded.
        RuleFor(x => x.Gateway)
            .NotEmpty()
            .When(x => x.RefundType != "CreditBalance")
            .WithMessage("Gateway is required for gateway and manual refunds.");

        RuleFor(x => x.Gateway).MaximumLength(100).When(x => x.Gateway is not null);
        RuleFor(x => x.RefundTransactionId).NotEmpty()
            .When(x => x.RefundType == "Manual")
            .WithMessage("RefundTransactionId is required for Manual refund type.");
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
