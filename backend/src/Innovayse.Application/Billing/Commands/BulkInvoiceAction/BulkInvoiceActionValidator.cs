namespace Innovayse.Application.Billing.Commands.BulkInvoiceAction;

using FluentValidation;

/// <summary>Validates <see cref="BulkInvoiceActionCommand"/>.</summary>
public sealed class BulkInvoiceActionValidator : AbstractValidator<BulkInvoiceActionCommand>
{
    /// <summary>All valid bulk action names.</summary>
    private static readonly string[] ValidActions =
        ["MarkPaid", "MarkUnpaid", "MarkCancelled", "Duplicate", "Delete"];

    /// <summary>Initialises all validation rules.</summary>
    public BulkInvoiceActionValidator()
    {
        RuleFor(x => x.InvoiceIds).NotEmpty();
        RuleForEach(x => x.InvoiceIds).GreaterThan(0);
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => ValidActions.Contains(a))
            .WithMessage($"'Action' must be one of: {string.Join(", ", ValidActions)}.");
    }
}
