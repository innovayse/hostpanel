namespace Innovayse.Application.Clients.Commands.UpdateContact;

using FluentValidation;

/// <summary>Validates <see cref="UpdateContactCommand"/> before the handler executes.</summary>
public sealed class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    /// <summary>Initialises validation rules for contact update.</summary>
    public UpdateContactValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.ContactId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => x.CompanyName is not null);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(50).When(x => x.Phone is not null);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Street).MaximumLength(200).When(x => x.Street is not null);
        RuleFor(x => x.Address2).MaximumLength(200).When(x => x.Address2 is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.State).MaximumLength(100).When(x => x.State is not null);
        RuleFor(x => x.PostCode).MaximumLength(20).When(x => x.PostCode is not null);
        RuleFor(x => x.Country).Length(2).When(x => x.Country is not null);
    }
}
