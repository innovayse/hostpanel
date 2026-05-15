namespace Innovayse.Infrastructure.Domains;

using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IDomainRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class DomainRepository(AppDbContext db) : IDomainRepository
{
    /// <inheritdoc/>
    public async Task<Domain?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Domains
            .Include(x => x.Nameservers)
            .Include(x => x.DnsRecords)
            .Include(x => x.EmailForwardingRules)
            .Include(x => x.Reminders)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<Domain?> FindByNameAsync(string name, CancellationToken ct) =>
        await db.Domains
            .Include(x => x.Nameservers)
            .Include(x => x.DnsRecords)
            .Include(x => x.EmailForwardingRules)
            .Include(x => x.Reminders)
            .FirstOrDefaultAsync(x => x.Name == name, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Domain>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.Domains
            .Include(x => x.Nameservers)
            .Include(x => x.DnsRecords)
            .Include(x => x.EmailForwardingRules)
            .Include(x => x.Reminders)
            .Where(x => x.ClientId == clientId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Domain>> ListExpiringBeforeAsync(DateTimeOffset threshold, CancellationToken ct) =>
        await db.Domains
            .Include(x => x.Nameservers)
            .Include(x => x.DnsRecords)
            .Include(x => x.EmailForwardingRules)
            .Include(x => x.Reminders)
            .Where(x => x.ExpiresAt < threshold && x.Status == DomainStatus.Active)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Domain>> ListAutoRenewDueAsync(DateTimeOffset threshold, CancellationToken ct) =>
        await db.Domains
            .Include(x => x.Nameservers)
            .Include(x => x.DnsRecords)
            .Include(x => x.EmailForwardingRules)
            .Include(x => x.Reminders)
            .Where(x => x.AutoRenew && x.ExpiresAt < threshold && x.Status == DomainStatus.Active)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Domain> Items, int TotalCount)> PagedListAsync(
        int page, int pageSize, CancellationToken ct, int? clientId = null)
    {
        var query = db.Domains.AsQueryable();

        if (clientId.HasValue)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var ordered = query.OrderBy(x => x.Name);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, string>> FindDomainNamesByServiceIdsAsync(
        IEnumerable<int> serviceIds, CancellationToken ct)
    {
        var idList = serviceIds.ToList();
        return await db.Domains
            .Where(d => d.LinkedServiceId != null && idList.Contains(d.LinkedServiceId.Value))
            .ToDictionaryAsync(d => d.LinkedServiceId!.Value, d => d.Name, ct);
    }

    /// <inheritdoc/>
    public void Add(Domain domain) => db.Domains.Add(domain);
}
