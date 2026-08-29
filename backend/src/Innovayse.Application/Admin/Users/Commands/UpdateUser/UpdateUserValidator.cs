namespace Innovayse.Application.Admin.Users.Commands.UpdateUser;

using FluentValidation;

/// <summary>Validates <see cref="UpdateUserCommand"/> before it is handled.</summary>
public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    /// <summary>Initialises all validation rules for the profile update.</summary>
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("User id is required.");
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.").MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required.").MaximumLength(100);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(256);
    }
}
