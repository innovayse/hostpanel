using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IClientRecordStore"/>: inserts the record and, when a unique
/// index on the user or on the external account refuses it, answers with the row that won instead.
/// </summary>
public sealed class ClientRecordStore : IClientRecordStore
{
    /// <summary>PostgreSQL SQLSTATE for a unique constraint violation.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>The application DbContext.</summary>
    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ClientRecordStore"/> class.</summary>
    /// <param name="db">The application DbContext.</param>
    public ClientRecordStore(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<ClientRecordWriteResult> AddOrGetAsync(Client candidate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        _db.Clients.Add(candidate);
        try
        {
            await _db.SaveChangesAsync(ct);
            return new ClientRecordWriteResult(candidate, Created: true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Another writer inserted this user's row, or took the external account the candidate is
            // linked to (a second member of the same account ordering at the same moment), between our
            // lookup and our insert. Drop the rejected candidate from the tracker so the next save does
            // not retry it, then read the winner. If the lookup finds nothing the violation was on some
            // other index, and that is a real failure, so it is rethrown.
            _db.Entry(candidate).State = EntityState.Detached;
            var provider = candidate.ExternalAccountProvider;
            var accountId = candidate.ExternalAccountId;
            var existing = await _db.Clients.FirstOrDefaultAsync(
                c => c.UserId == candidate.UserId
                    || (provider != null && c.ExternalAccountProvider == provider && c.ExternalAccountId == accountId),
                ct);
            if (existing is null)
            {
                throw;
            }

            return new ClientRecordWriteResult(existing, Created: false);
        }
    }
}
