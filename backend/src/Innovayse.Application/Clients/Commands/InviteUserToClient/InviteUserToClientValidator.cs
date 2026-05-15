namespace Innovayse.Application.Clients.Commands.InviteUserToClient;

using FluentValidation;

/// <summary>Validates <see cref="InviteUserToClientCommand"/> before the handler executes.</summary>
public sealed class InviteUserToClientValidator : AbstractValidator<InviteUserToClientCommand>
{
    /// <summary>Initialises validation rules for user invitation.</summary>
    public InviteUserToClientValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}
