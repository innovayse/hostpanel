namespace Innovayse.Application.Auth.Commands.Login;

using FluentValidation;

/// <summary>Validates <see cref="LoginCommand"/> before the handler executes.</summary>
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    /// <summary>Initialises validation rules for login.</summary>
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
