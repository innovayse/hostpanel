namespace Innovayse.Application.Billing.Commands.InvoiceSelectedItems;

using FluentValidation;

/// <summary>Validates <see cref="InvoiceSelectedItemsCommand"/> before it reaches the handler.</summary>
public sealed class InvoiceSelectedItemsValidator : AbstractValidator<InvoiceSelectedItemsCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public InvoiceSelectedItemsValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.BillableItemIds).NotEmpty();
    }
}
