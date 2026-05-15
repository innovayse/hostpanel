namespace Innovayse.Application.Clients.Commands.UpdateClient;

using FluentValidation;

/// <summary>Validates <see cref="UpdateClientCommand"/> before the handler executes.</summary>
public sealed class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    /// <summary>Initialises validation rules for client update.</summary>
    public UpdateClientValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => x.CompanyName is not null);
        RuleFor(x => x.Phone).MaximumLength(50).When(x => x.Phone is not null);
        RuleFor(x => x.Street).MaximumLength(200).When(x => x.Street is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.State).MaximumLength(100).When(x => x.State is not null);
        RuleFor(x => x.PostCode).MaximumLength(20).When(x => x.PostCode is not null);
        RuleFor(x => x.Country).Length(2).When(x => x.Country is not null);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
        RuleFor(x => x.PaymentMethod).MaximumLength(50).When(x => x.PaymentMethod is not null);
        RuleFor(x => x.BillingContact).MaximumLength(256).When(x => x.BillingContact is not null);
        RuleFor(x => x.AdminNotes).MaximumLength(2000).When(x => x.AdminNotes is not null);
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<Domain.Clients.ClientStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be one of: Active, Inactive, Suspended, Closed.")
            .When(x => x.Status is not null);
    }
}
