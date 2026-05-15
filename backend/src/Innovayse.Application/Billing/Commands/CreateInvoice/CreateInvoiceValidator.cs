namespace Innovayse.Application.Billing.Commands.CreateInvoice;

using FluentValidation;

/// <summary>Validates <see cref="CreateInvoiceCommand"/> before it reaches the handler.</summary>
public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.DueDate)
            .Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("'Due Date' must be in the future.");
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(500);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
