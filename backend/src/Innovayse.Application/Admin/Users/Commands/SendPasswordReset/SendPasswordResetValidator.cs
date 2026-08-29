namespace Innovayse.Application.Admin.Users.Commands.SendPasswordReset;

using FluentValidation;

/// <summary>
/// Validates <see cref="SendPasswordResetCommand"/>. Its only field is the route-supplied
/// subject, so the single rule guards against an empty id; there is deliberately nothing
/// further to check.
/// </summary>
public sealed class SendPasswordResetValidator : AbstractValidator<SendPasswordResetCommand>
{
    /// <summary>Initialises the validator.</summary>
    public SendPasswordResetValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("User id is required.");
    }
}
