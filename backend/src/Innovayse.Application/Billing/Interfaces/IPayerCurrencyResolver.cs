namespace Innovayse.Application.Billing.Interfaces;

using Innovayse.Domain.Billing;

/// <summary>
/// The one answer to "which currency is this payer billed in".
/// </summary>
/// <remarks>
/// Used wherever an invoice is created, a price is looked up, a gateway list is built or a
/// gateway payment is started. Having it in one place is what stops the list offering a method
/// the start then refuses, or a product showing a price the invoice then bills differently.
/// Every method returns the full <see cref="Currency"/> row so callers can format and charge
/// without a second lookup.
/// </remarks>
public interface IPayerCurrencyResolver
{
    /// <summary>The currency a given client is billed in: their own, else the base.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The currency.</returns>
    Task<Currency> ForClientAsync(int clientId, CancellationToken ct);

    /// <summary>The currency the current caller is billed in: their client's, else the base (a guest).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The currency.</returns>
    Task<Currency> ForCallerAsync(CancellationToken ct);

    /// <summary>The base currency.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The base currency.</returns>
    Task<Currency> BaseAsync(CancellationToken ct);
}
