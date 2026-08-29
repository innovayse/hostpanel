namespace Innovayse.Application.Admin.Users.Commands.DeleteUser;

using FluentValidation;

/// <summary>
/// Validates <see cref="DeleteUserCommand"/>. Its only field is the route-supplied subject, so
/// the single rule guards against an empty id; there is deliberately nothing further to check.
/// </summary>
public sealed class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    /// <summary>Initialises the validator.</summary>
    public DeleteUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("User id is required.");
    }
}
