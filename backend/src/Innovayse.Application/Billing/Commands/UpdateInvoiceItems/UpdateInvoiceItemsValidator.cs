namespace Innovayse.Application.Billing.Commands.UpdateInvoiceItems;

using FluentValidation;

/// <summary>Validates <see cref="UpdateInvoiceItemsCommand"/> before it reaches the handler.</summary>
public sealed class UpdateInvoiceItemsValidator : AbstractValidator<UpdateInvoiceItemsCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public UpdateInvoiceItemsValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(500);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
