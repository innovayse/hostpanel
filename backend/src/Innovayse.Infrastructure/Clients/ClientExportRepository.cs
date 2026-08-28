namespace Innovayse.Infrastructure.Clients;

using Innovayse.Application.Clients.Interfaces;
using Innovayse.Application.Clients.Queries.ExportClientData;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IClientExportRepository"/>.</summary>
/// <param name="db">The application DbContext.</param>
public sealed class ClientExportRepository(AppDbContext db) : IClientExportRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientExportBillableItemDto>> ListBillableItemsAsync(
        int clientId,
        CancellationToken ct = default) =>
        // Projected in SQL rather than materialised as aggregates: the export reads four columns
        // of each row and never touches the entity's behaviour.
        await db.BillableItems
            .Where(b => b.ClientId == clientId)
            .Select(b => new ClientExportBillableItemDto(b.Id, b.Description, b.Amount, b.NextDueDate))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientExportEmailDto>> ListEmailsForRecipientAsync(
        string recipient,
        CancellationToken ct = default) =>
        // The body is deliberately not selected — it is the largest column in the table and the
        // export only ever prints the envelope.
        await db.EmailLogs
            .Where(e => e.To == recipient)
            .Select(e => new ClientExportEmailDto(e.Id, e.Subject, e.SentAt, e.Success))
            .ToListAsync(ct);
}
