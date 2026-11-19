using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>Resolves a payer's billing currency from their client record, falling back to the base.</summary>
public sealed class PayerCurrencyResolver : IPayerCurrencyResolver
{
    /// <summary>The module's data context, for the recorded client currency and the configured currencies.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the current request's caller acts for, owner or linked user.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the recorded client currency and the configured currencies.</param>
    /// <param name="callerClient">Resolves the account the current request's caller acts for, owner or linked user.</param>
    public PayerCurrencyResolver(IHostpanelDbContext context, ICallerClientResolver callerClient)
    {
        _context = context;
        _callerClient = callerClient;
    }

    /// <inheritdoc/>
    public async Task<Domain.Billing.Currency> ForClientAsync(int clientId, CancellationToken ct)
    {
        // Read-only, so no tracking is needed.
        var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        return await ResolveAsync(client?.Currency, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, Domain.Billing.Currency>> ForClientsAsync(
        IReadOnlyCollection<int> clientIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clientIds);

        var recorded = await _context.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Currency })
            .ToDictionaryAsync(c => c.Id, c => c.Currency, ct);
        var configured = await _context.Currencies.AsNoTracking().ToDictionaryAsync(c => c.Code, ct);
        var baseCurrency = await _context.GetBaseCurrencyAsync(ct);

        // The same fallback as ResolveAsync: no recorded code, or one that is not configured, bills in the base.
        return clientIds.Distinct().ToDictionary(
            id => id,
            id => recorded.GetValueOrDefault(id) is { Length: > 0 } code
                && configured.TryGetValue(code.ToUpperInvariant(), out var currency)
                    ? currency
                    : baseCurrency);
    }

    /// <inheritdoc/>
    public async Task<Domain.Billing.Currency> ForCallerAsync(CancellationToken ct)
    {
        var access = await _callerClient.ResolveAsync(ct);
        if (access.ClientId is not { } clientId)
        {
            return await _context.GetBaseCurrencyAsync(ct);
        }

        return await ForClientAsync(clientId, ct);
    }

    /// <inheritdoc/>
    public Task<Domain.Billing.Currency> BaseAsync(CancellationToken ct) => _context.GetBaseCurrencyAsync(ct);

    /// <summary>
    /// Maps a recorded alpha code to its configured row, or to the base when there is none.
    /// </summary>
    /// <remarks>
    /// A client can carry a code that was never configured: imported data, or a currency an
    /// admin later removed. Billing them in it would produce an invoice nothing can format or
    /// charge, so the base is used. The condition is worth an admin's attention, which is why
    /// the client form (Plan 2) flags a code that is not in the list.
    /// </remarks>
    /// <param name="recorded">The client's recorded code, or null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The configured currency.</returns>
    private async Task<Domain.Billing.Currency> ResolveAsync(string? recorded, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recorded))
        {
            return await _context.GetBaseCurrencyAsync(ct);
        }

        return await _context.FindCurrencyAsync(recorded, ct) ?? await _context.GetBaseCurrencyAsync(ct);
    }
}
