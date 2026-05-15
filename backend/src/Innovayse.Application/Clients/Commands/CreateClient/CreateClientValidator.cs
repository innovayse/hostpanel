namespace Innovayse.Application.Clients.Commands.CreateClient;

using FluentValidation;

/// <summary>Validates <see cref="CreateClientCommand"/> before the handler executes.</summary>
public sealed class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    /// <summary>Initialises validation rules for client creation.</summary>
    public CreateClientValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => x.CompanyName is not null);
    }
}
