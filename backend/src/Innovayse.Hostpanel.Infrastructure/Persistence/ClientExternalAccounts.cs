using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IClientExternalAccounts"/>: a read of the link columns of the client row.</summary>
public sealed class ClientExternalAccounts : IClientExternalAccounts
{
    /// <summary>The application DbContext the client is read through.</summary>
    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ClientExternalAccounts"/> class.</summary>
    /// <param name="db">The application DbContext the client is read through.</param>
    public ClientExternalAccounts(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<ExternalAccount?> FindAsync(int clientId, CancellationToken ct)
    {
        var link = await _db.Clients
            .AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.ExternalAccountProvider, c.ExternalAccountId })
            .SingleOrDefaultAsync(ct);

        // The two columns are set and cleared together (a check constraint), so one without the other never exists.
        return link?.ExternalAccountProvider is { } provider && link.ExternalAccountId is { } id
            ? new ExternalAccount(provider, id, null, null)
            : null;
    }
}
