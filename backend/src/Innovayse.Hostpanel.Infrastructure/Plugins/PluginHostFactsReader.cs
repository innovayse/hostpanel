using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;

/*
 * The plugin host API is the host's own answer to an SDK contract, not a use case of one module, so the rows of the
 * Clients, Services and Billing modules it shares are read here, behind a port, and never from the Plugins module. Each
 * list is cut in SQL (Take) and projected to the record's columns only, so no note, card id or address leaves the query
 * and a client with thousands of rows never loads more than the SDK's 50.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>Reads the client, service and invoice rows the plugin host API shares about one client.</summary>
public sealed class PluginHostFactsReader : IPluginHostFactsReader
{
    /// <summary>The request's data context, read without tracking.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the reader.</summary>
    /// <param name="context">The request's data context, read without tracking.</param>
    public PluginHostFactsReader(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<PluginHostClientDto?> FindClientAsync(int clientId, CancellationToken ct)
    {
        return _context.Clients
            .AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new PluginHostClientDto(c.FirstName, c.LastName, c.CompanyName, c.UserId))
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginHostServiceDto>> ListNewestServicesAsync(int clientId, CancellationToken ct)
    {
        return await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.ClientId == clientId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Take(PluginHostLimits.MaxListed)
            .Select(s => new PluginHostServiceDto(s.Id, s.ProductId, s.Status, s.Domain, s.CreatedAt, s.NextRenewalAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginHostInvoiceDto>> ListNewestInvoicesAsync(int clientId, CancellationToken ct)
    {
        // The same filter the client portal uses: a draft is staff-only and is never shared.
        return await _context.Invoices
            .AsNoTracking()
            .Where(Invoice.ClientVisibleFilter)
            .Where(i => i.ClientId == clientId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(PluginHostLimits.MaxListed)
            .Select(i => new PluginHostInvoiceDto(i.Id, i.Status, i.Total, i.Currency, i.InvoiceDate, i.DueDate, i.PaidAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListActiveServiceDomainsAsync(
        int clientId, IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var domains = await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.ClientId == clientId && s.Status == ServiceStatus.Active && s.Domain != null && productIds.Contains(s.ProductId))
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Take(PluginHostLimits.MaxListed)
            .Select(s => s.Domain)
            .ToListAsync(ct);

        return domains.OfType<string>().ToList();
    }
}
