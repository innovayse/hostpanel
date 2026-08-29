namespace Innovayse.Application.Billing.Commands.CreateInvoice;

using FluentValidation;

/// <summary>Validates <see cref="CreateInvoiceCommand"/> before it reaches the handler.</summary>
public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        // Compared by date, not by instant. The rule used to read `d > DateTimeOffset.UtcNow`,
        // and the admin's date picker sends a date-only value that binds to midnight UTC -- so
        // "due today", an ordinary thing to raise an invoice for, was always already in the past
        // and would have been refused the moment this validator started running.
        RuleFor(x => x.DueDate)
            .Must(d => d.UtcDateTime.Date >= DateTime.UtcNow.Date)
            .WithMessage("'Due Date' must not be in the past.");
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(500);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
