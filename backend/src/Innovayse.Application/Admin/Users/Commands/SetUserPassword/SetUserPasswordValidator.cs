namespace Innovayse.Application.Admin.Users.Commands.SetUserPassword;

using FluentValidation;

/// <summary>Validates <see cref="SetUserPasswordCommand"/> before it is handled.</summary>
public sealed class SetUserPasswordValidator : AbstractValidator<SetUserPasswordCommand>
{
    /// <summary>Initialises all validation rules for the password change.</summary>
    public SetUserPasswordValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("User id is required.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}
