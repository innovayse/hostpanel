namespace Innovayse.Application.Clients.Commands.AcceptInvitation;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="AcceptInvitationCommand"/>.
/// Validates the invitation token, creates an Identity user with the specified password,
/// links the user to the client account with the invitation's permissions, and returns the
/// new user ID so the API layer can issue an auth token for immediate login.
/// </summary>
/// <param name="invitationRepo">Invitation repository for token lookup.</param>
/// <param name="clientRepo">Client repository for loading the client account.</param>
/// <param name="userService">Identity user service for account creation and role assignment.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
public sealed class AcceptInvitationHandler(
    IInvitationRepository invitationRepo,
    IClientRepository clientRepo,
    IUserService userService,
    IUnitOfWork uow)
{
    /// <summary>
    /// Accepts the invitation, creates a user account, links it to the client, and returns the user ID.
    /// </summary>
    /// <param name="cmd">The accept invitation command with token and password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created Identity user ID for token generation by the API layer.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the invitation token is not found, the invitation is expired or already accepted,
    /// or the client account is not found.
    /// </exception>
    public async Task<string> HandleAsync(AcceptInvitationCommand cmd, CancellationToken ct)
    {
        var invitation = await invitationRepo.FindByTokenAsync(cmd.Token, ct)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");

        // Validate invitation is still valid (don't mark accepted yet — user creation might fail)
        if (invitation.IsExpired)
            throw new InvalidOperationException("This invitation has expired.");
        if (invitation.IsAccepted)
            throw new InvalidOperationException("This invitation has already been accepted.");

        // Create the Identity user first — this can fail on password validation
        var userId = await userService.CreateAsync(invitation.Email, cmd.Password, ct);

        // Update the user's name
        await userService.UpdateUserAsync(userId, invitation.FirstName, invitation.LastName, invitation.Email, null, ct);

        // Add to the Client role
        await userService.AddToRoleAsync(userId, Roles.Client, ct);

        // Load the client and link the new user with the invitation's permissions
        var client = await clientRepo.FindByIdAsync(invitation.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {invitation.ClientId} not found.");

        client.AddUser(userId, invitation.Permissions);

        // Mark accepted only after everything succeeded
        invitation.Accept();

        await uow.SaveChangesAsync(ct);

        return userId;
    }
}
