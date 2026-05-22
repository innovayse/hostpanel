namespace Innovayse.Application.Billing.Commands.UpdateInvoiceNotes;

using FluentValidation;

/// <summary>Validates <see cref="UpdateInvoiceNotesCommand"/>.</summary>
public sealed class UpdateInvoiceNotesValidator : AbstractValidator<UpdateInvoiceNotesCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public UpdateInvoiceNotesValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
    }
}
