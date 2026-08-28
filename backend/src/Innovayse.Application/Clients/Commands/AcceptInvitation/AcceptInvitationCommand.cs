namespace Innovayse.Application.Clients.Commands.AcceptInvitation;

/// <summary>Command to accept an invitation and link the SSO-authenticated user to the client account.</summary>
/// <remarks>
/// Carries no user id. Who is accepting is resolved inside the handler from the credential:
/// a field naming the subject would let whoever holds an invitation token link a different
/// account to the client, permissions and all.
/// </remarks>
/// <param name="Token">The invitation token.</param>
public record AcceptInvitationCommand(string Token);
