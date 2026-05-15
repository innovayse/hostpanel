namespace Innovayse.Application.Clients.Commands.AcceptInvitation;

/// <summary>Command to accept an invitation and create the user account.</summary>
/// <param name="Token">The invitation token.</param>
/// <param name="Password">The password chosen by the invitee.</param>
public record AcceptInvitationCommand(string Token, string Password);
