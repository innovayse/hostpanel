namespace Innovayse.Application.Billing.Commands.UpdateInvoiceOptions;

using FluentValidation;

/// <summary>Validates <see cref="UpdateInvoiceOptionsCommand"/>.</summary>
public sealed class UpdateInvoiceOptionsValidator : AbstractValidator<UpdateInvoiceOptionsCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public UpdateInvoiceOptionsValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100);
        RuleFor(x => x.PaymentMethod).MaximumLength(100).When(x => x.PaymentMethod is not null);
    }
}
