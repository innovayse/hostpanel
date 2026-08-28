namespace Innovayse.Application.Clients.Commands.AcceptInvitation;

using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="AcceptInvitationCommand"/>.
/// Validates the invitation token, links the already-authenticated caller to the client
/// account with the invitation's permissions, and returns their subject.
/// </summary>
/// <param name="invitationRepo">Invitation repository for token lookup.</param>
/// <param name="clientRepo">Client repository for loading the client account.</param>
/// <param name="roles">Role store, for granting the Client role.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
/// <param name="caller">Who is accepting; the command does not say, and must not.</param>
public sealed class AcceptInvitationHandler(
    IInvitationRepository invitationRepo,
    IClientRepository clientRepo,
    ISubjectRoleStore roles,
    IUnitOfWork uow,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Accepts the invitation, links the caller to the client, and returns their subject.
    /// </summary>
    /// <param name="cmd">The accept invitation command, carrying the token only.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's subject, unchanged.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the invitation token is not found, the invitation is expired or already accepted,
    /// or the client account is not found.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<string> HandleAsync(AcceptInvitationCommand cmd, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var invitation = await invitationRepo.FindByTokenAsync(cmd.Token, ct)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");

        // Validate invitation is still valid (don't mark accepted yet — user creation might fail)
        if (invitation.IsExpired)
        {
            throw new InvalidOperationException("This invitation has expired.");
        }

        if (invitation.IsAccepted)
        {
            throw new InvalidOperationException("This invitation has already been accepted.");
        }

        // The caller is already signed in, and this handler asked the credential who they
        // are rather than being told. Nothing here writes to their account: the invitation
        // carries a name and address only so the email that delivered it could be
        // addressed, and copying those over the account's own details would let whoever
        // sent the invitation rename the person who accepted it.
        await roles.AddAsync(userId, Roles.Client, ct);

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
