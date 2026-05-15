namespace Innovayse.Infrastructure.Clients;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IClientRepository"/>.
/// Operates on the <c>clients</c> and <c>contacts</c> tables via <see cref="AppDbContext"/>.
/// </summary>
/// <param name="db">The application DbContext.</param>
public sealed class ClientRepository(AppDbContext db) : IClientRepository
{
    /// <inheritdoc/>
    public async Task<Client?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Clients
            .Include(c => c.Contacts)
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public async Task<Client?> FindByUserIdAsync(string userId, CancellationToken ct) =>
        await db.Clients
            .Include(c => c.Contacts)
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search,
        string? phone, ClientStatus? status,
        IEnumerable<string>? userIds,
        CancellationToken ct)
    {
        var query = db.Clients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var phoneTerm = phone.ToLower();
            query = query.Where(c => c.Phone != null && c.Phone.ToLower().Contains(phoneTerm));
        }

        if (status is not null)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (userIds is not null)
        {
            var idList = userIds.ToList();
            query = query.Where(c => idList.Contains(c.UserId));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public void Add(Client client) => db.Clients.Add(client);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Client>> GetCreatedBetweenAsync(
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await db.Clients
            .Where(c => c.CreatedAt >= start && c.CreatedAt <= end)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<Dictionary<string, int>> FindClientIdsByUserIdsAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var idList = userIds.ToList();
        return await db.Clients
            .Where(c => idList.Contains(c.UserId))
            .ToDictionaryAsync(c => c.UserId, c => c.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAllAsync(CancellationToken ct) =>
        await db.Clients.CountAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Client>> FindByIdsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        return await db.Clients
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(ct);
    }
}
