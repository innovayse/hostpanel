namespace Innovayse.Infrastructure.Clients;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IInvitationRepository"/>.</summary>
/// <param name="db">The application DbContext.</param>
public sealed class InvitationRepository(AppDbContext db) : IInvitationRepository
{
    /// <inheritdoc/>
    public async Task<Invitation?> FindByTokenAsync(string token, CancellationToken ct) =>
        await db.Invitations.FirstOrDefaultAsync(i => i.Token == token, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invitation>> FindByClientIdAsync(int clientId, CancellationToken ct) =>
        await db.Invitations.Where(i => i.ClientId == clientId).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<Invitation?> FindPendingByEmailAndClientAsync(string email, int clientId, CancellationToken ct) =>
        await db.Invitations.FirstOrDefaultAsync(
            i => i.Email == email
                 && i.ClientId == clientId
                 && i.AcceptedAt == null
                 && i.ExpiresAt > DateTimeOffset.UtcNow,
            ct);

    /// <inheritdoc/>
    public void Add(Invitation invitation) => db.Invitations.Add(invitation);
}
