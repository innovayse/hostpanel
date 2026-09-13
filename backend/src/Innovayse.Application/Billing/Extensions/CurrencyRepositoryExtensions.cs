namespace Innovayse.Application.Billing.Extensions;

using Innovayse.Domain.Billing.Interfaces;

/// <summary>Questions handlers ask <see cref="ICurrencyRepository"/> that it does not answer by itself.</summary>
public static class CurrencyRepositoryExtensions
{
    /// <summary>Whether a code names a currency this panel is currently offering to clients.</summary>
    /// <remarks>
    /// "Offering" is both halves: configured, and switched on. A currency an operator set up and
    /// then disabled keeps the clients it already has (<c>Currency.Disable</c> says so) but may
    /// not be given to a new one, and neither may a code the panel has never heard of. Every
    /// place a client's currency is chosen — the admin form, guest checkout — asks this one
    /// question so that the three cannot drift apart on what "available" means.
    /// </remarks>
    /// <param name="currencies">The configured currencies.</param>
    /// <param name="code">ISO 4217 alpha code, any case; looked up in the upper case it is stored in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the currency exists and is enabled.</returns>
    public static async Task<bool> IsOfferedAsync(this ICurrencyRepository currencies, string code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currencies);
        ArgumentNullException.ThrowIfNull(code);

        var currency = await currencies.FindAsync(code.ToUpperInvariant(), ct);
        return currency is { IsEnabled: true };
    }
}
