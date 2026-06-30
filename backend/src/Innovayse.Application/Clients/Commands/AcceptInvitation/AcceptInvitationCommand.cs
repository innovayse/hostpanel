namespace Innovayse.Application.Clients.Commands.AcceptInvitation;

/// <summary>Command to accept an invitation and link the SSO-authenticated user to the client account.</summary>
/// <param name="Token">The invitation token.</param>
/// <param name="UserId">The local user ID of the SSO-authenticated caller.</param>
public record AcceptInvitationCommand(string Token, string UserId);
