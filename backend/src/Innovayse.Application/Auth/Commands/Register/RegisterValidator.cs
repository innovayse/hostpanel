namespace Innovayse.Application.Auth.Commands.Register;

using FluentValidation;

/// <summary>Validates <see cref="RegisterCommand"/> before the handler executes.</summary>
public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>Initialises validation rules for registration.</summary>
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);
    }
}
