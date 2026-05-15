namespace Innovayse.Infrastructure.Clients;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IClientUserRepository"/>.</summary>
/// <param name="db">The application DbContext.</param>
public sealed class ClientUserRepository(AppDbContext db) : IClientUserRepository
{
    /// <inheritdoc/>
    public async Task<ClientUser?> FindAsync(string userId, int clientId, CancellationToken ct) =>
        await db.ClientUsers.FirstOrDefaultAsync(cu => cu.UserId == userId && cu.ClientId == clientId, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientUser>> FindByClientIdAsync(int clientId, CancellationToken ct) =>
        await db.ClientUsers.Where(cu => cu.ClientId == clientId).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientUser>> FindByUserIdAsync(string userId, CancellationToken ct) =>
        await db.ClientUsers.Where(cu => cu.UserId == userId).ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(ClientUser clientUser) => db.ClientUsers.Add(clientUser);

    /// <inheritdoc/>
    public void Remove(ClientUser clientUser) => db.ClientUsers.Remove(clientUser);
}
