namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>Persistence for <see cref="Currency"/>.</summary>
public interface ICurrencyRepository
{
    /// <summary>Finds a currency by alpha code, case-insensitively.</summary>
    /// <param name="code">ISO 4217 alpha code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The currency, or <see langword="null"/>.</returns>
    Task<Currency?> FindAsync(string code, CancellationToken ct);

    /// <summary>Returns the base currency. There is always exactly one.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The base currency.</returns>
    Task<Currency> GetBaseAsync(CancellationToken ct);

    /// <summary>Lists currencies, base first, then by code.</summary>
    /// <param name="enabledOnly">Whether to return only enabled currencies.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The currencies.</returns>
    Task<IReadOnlyList<Currency>> ListAsync(bool enabledOnly, CancellationToken ct);

    /// <summary>Tracks a new currency for insertion.</summary>
    /// <param name="currency">The currency.</param>
    void Add(Currency currency);
}
