namespace Innovayse.Application.Billing.Commands.PublishInvoice;

using FluentValidation;

/// <summary>Validates <see cref="PublishInvoiceCommand"/>.</summary>
public sealed class PublishInvoiceValidator : AbstractValidator<PublishInvoiceCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public PublishInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
    }
}
