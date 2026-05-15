namespace Innovayse.Application.Clients.Commands.AcceptInvitation;

using FluentValidation;

/// <summary>Validates <see cref="AcceptInvitationCommand"/> before the handler executes.</summary>
public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    /// <summary>Initialises validation rules for invitation acceptance.</summary>
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
