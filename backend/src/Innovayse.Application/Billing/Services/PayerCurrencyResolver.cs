namespace Innovayse.Application.Billing.Services;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Resolves a payer's billing currency from their client record, falling back to the base.</summary>
/// <param name="currencies">The configured currencies.</param>
/// <param name="clients">Client records, for the recorded currency.</param>
/// <param name="caller">The current request's caller.</param>
public sealed class PayerCurrencyResolver(
    ICurrencyRepository currencies,
    IClientRepository clients,
    ICurrentRequestContext caller) : IPayerCurrencyResolver
{
    /// <inheritdoc/>
    public async Task<Currency> ForClientAsync(int clientId, CancellationToken ct)
    {
        var client = await clients.FindByIdAsync(clientId, ct);
        return await ResolveAsync(client?.Currency, ct);
    }

    /// <inheritdoc/>
    public async Task<Currency> ForCallerAsync(CancellationToken ct)
    {
        if (caller.UserId is not { } userId)
        {
            return await currencies.GetBaseAsync(ct);
        }

        var client = await clients.FindByUserIdAsync(userId, ct);
        return await ResolveAsync(client?.Currency, ct);
    }

    /// <inheritdoc/>
    public Task<Currency> BaseAsync(CancellationToken ct) => currencies.GetBaseAsync(ct);

    /// <summary>
    /// Maps a recorded alpha code to its configured row, or to the base when there is none.
    /// </summary>
    /// <remarks>
    /// A client can carry a code that was never configured -- imported data, or a currency an
    /// admin later removed. Billing them in it would produce an invoice nothing can format or
    /// charge, so the base is used. The condition is worth an admin's attention, which is why
    /// the client form (Plan 2) flags a code that is not in the list.
    /// </remarks>
    /// <param name="recorded">The client's recorded code, or null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The configured currency.</returns>
    private async Task<Currency> ResolveAsync(string? recorded, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recorded))
        {
            return await currencies.GetBaseAsync(ct);
        }

        return await currencies.FindAsync(recorded, ct) ?? await currencies.GetBaseAsync(ct);
    }
}
