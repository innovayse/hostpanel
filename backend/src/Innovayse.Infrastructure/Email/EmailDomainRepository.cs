namespace Innovayse.Infrastructure.Email;

using Innovayse.Domain.Email;
using Innovayse.Domain.Email.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IEmailDomainRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class EmailDomainRepository(AppDbContext db) : IEmailDomainRepository
{
    /// <inheritdoc/>
    public async Task<EmailDomain?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.EmailDomains
            .Include(x => x.Mailboxes)
            .Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<EmailDomain?> FindByDomainNameAsync(string domainName, CancellationToken ct) =>
        await db.EmailDomains
            .Include(x => x.Mailboxes)
            .Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.DomainName == domainName.ToLowerInvariant(), ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmailDomain>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.EmailDomains
            .Include(x => x.Mailboxes)
            .Where(x => x.ClientId == clientId)
            .OrderBy(x => x.DomainName)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(EmailDomain emailDomain) => db.EmailDomains.Add(emailDomain);
}
