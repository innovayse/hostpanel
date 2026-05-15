namespace Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Repository for managing <see cref="Invitation"/> entities — pending user invitations to client accounts.
/// </summary>
public interface IInvitationRepository
{
    /// <summary>Finds an invitation by its unique token.</summary>
    /// <param name="token">The invitation token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Invitation"/>, or <see langword="null"/> if not found.</returns>
    Task<Invitation?> FindByTokenAsync(string token, CancellationToken ct);

    /// <summary>Finds all invitations for a given client account.</summary>
    /// <param name="clientId">The client account ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of invitations for the client.</returns>
    Task<IReadOnlyList<Invitation>> FindByClientIdAsync(int clientId, CancellationToken ct);

    /// <summary>Finds a pending (not accepted, not expired) invitation for a specific email and client.</summary>
    /// <param name="email">The invitee's email address.</param>
    /// <param name="clientId">The client account ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching pending <see cref="Invitation"/>, or <see langword="null"/> if none exists.</returns>
    Task<Invitation?> FindPendingByEmailAndClientAsync(string email, int clientId, CancellationToken ct);

    /// <summary>Adds a new invitation to the repository.</summary>
    /// <param name="invitation">The invitation to add.</param>
    void Add(Invitation invitation);
}
