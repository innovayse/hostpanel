using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.SDK.Plugins.Host;
using Microsoft.EntityFrameworkCore;

/*
 * The facts are read straight from the tables (as PayerCurrencyResolver does) because this adapter is the host's own
 * answer to an SDK contract, not a use case of one module. Each list is cut in SQL (Take), so a client with thousands
 * of rows never loads more than the SDK's 50, and only the columns the SDK record carries are selected: no notes, card
 * ids or addresses leave the query.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The SDK's <see cref="IPluginHostApi"/> for one call, bound when it is built to the call's client: no method takes an
/// id, so a plugin reads only the facts of that client. Draft invoices are hidden (they are staff-only, the same filter
/// the client portal uses), each list is the newest <see cref="PluginHostLimits.MaxListed"/> rows, and a client row that
/// does not exist answers like a call without a client.
/// </summary>
internal sealed class PluginHostApi : IPluginHostApi
{
    /// <summary>The call's client, or <see langword="null"/> when the call has none.</summary>
    private readonly int? _clientId;

    /// <summary>The request's data context.</summary>
    private readonly AppDbContext _context;

    /// <summary>The identity provider that holds the client's sign-in email and language.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Resolves the currency the client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes the facts of one call.</summary>
    /// <param name="clientId">The call's client, or <see langword="null"/> when the call has none.</param>
    /// <param name="context">The request's data context.</param>
    /// <param name="identity">The identity provider that holds the client's sign-in email and language.</param>
    /// <param name="payerCurrency">Resolves the currency the client is billed in.</param>
    public PluginHostApi(int? clientId, AppDbContext context, IIdentityProvider identity, IPayerCurrencyResolver payerCurrency)
    {
        _clientId = clientId;
        _context = context;
        _identity = identity;
        _payerCurrency = payerCurrency;
    }

    /// <inheritdoc/>
    public async Task<PluginClientProfile?> GetProfileAsync(CancellationToken ct)
    {
        if (_clientId is not { } id)
        {
            return null;
        }

        var row = await _context.Clients
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.FirstName, c.LastName, c.CompanyName, c.UserId })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var name = !string.IsNullOrWhiteSpace(row.CompanyName)
            ? row.CompanyName.Trim()
            : $"{row.FirstName} {row.LastName}".Trim();

        var account = string.IsNullOrEmpty(row.UserId)
            ? null
            : await _identity.FindBySubjectAsync(row.UserId, ct);

        var currency = await _payerCurrency.ForClientAsync(id, ct);

        return new PluginClientProfile(name, account?.Email, currency.Code, account?.Language);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginClientService>> ListServicesAsync(CancellationToken ct)
    {
        if (_clientId is not { } id)
        {
            return [];
        }

        var rows = await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.ClientId == id)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Take(PluginHostLimits.MaxListed)
            .Select(s => new { s.Id, s.ProductId, s.Status, s.Domain, s.CreatedAt, s.NextRenewalAt })
            .ToListAsync(ct);

        return rows
            .Select(s => new PluginClientService(s.Id, s.ProductId, PluginFactMapper.ToPlugin(s.Status), s.Domain, s.CreatedAt, s.NextRenewalAt))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginClientInvoice>> ListInvoicesAsync(CancellationToken ct)
    {
        if (_clientId is not { } id)
        {
            return [];
        }

        var rows = await _context.Invoices
            .AsNoTracking()
            .Where(Invoice.ClientVisibleFilter)
            .Where(i => i.ClientId == id)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(PluginHostLimits.MaxListed)
            .Select(i => new { i.Id, i.Status, i.Total, i.Currency, i.InvoiceDate, i.DueDate, i.PaidAt })
            .ToListAsync(ct);

        return rows
            .Select(i => new PluginClientInvoice(i.Id, PluginFactMapper.ToPlugin(i.Status), i.Total, i.Currency, i.InvoiceDate, i.DueDate, i.PaidAt))
            .ToList();
    }
}
