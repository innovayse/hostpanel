using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>Questions handlers ask the data context about currencies that it does not answer by itself.</summary>
public static class CurrencyExtensions
{
    /// <summary>Finds a currency by alpha code, case-insensitively.</summary>
    /// <remarks>Read-only, so no tracking is needed.</remarks>
    /// <param name="context">The module's data context.</param>
    /// <param name="code">ISO 4217 alpha code, any case; looked up in the upper case it is stored in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The currency, or <see langword="null"/>.</returns>
    public static Task<Currency?> FindCurrencyAsync(this IHostpanelDbContext context, string code, CancellationToken ct)
    {
        var upper = code.ToUpperInvariant();
        return context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == upper, ct);
    }

    /// <summary>Returns the base currency. There is always exactly one.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The base currency.</returns>
    /// <exception cref="InvalidOperationException">No row is marked as the base: a seed or migration failure, never a runtime state.</exception>
    public static async Task<Currency> GetBaseCurrencyAsync(this IHostpanelDbContext context, CancellationToken ct) =>
        await context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.IsBase, ct)
        ?? throw new InvalidOperationException("No base currency is configured.");

    /// <summary>Loads the multiplier that turns an amount in each configured currency into the base currency.</summary>
    /// <remarks>
    /// The one place the rates are read for any figure that adds amounts from different currencies
    /// (dashboard revenue, transaction totals), so they all convert the same way.
    /// </remarks>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rate to base by currency code.</returns>
    public static async Task<IReadOnlyDictionary<string, decimal>> GetRatesToBaseAsync(
        this IHostpanelDbContext context, CancellationToken ct) =>
        await context.Currencies.AsNoTracking().ToDictionaryAsync(c => c.Code, c => c.RateToBase, ct);

    /// <summary>Converts an amount to the base currency.</summary>
    /// <param name="ratesToBase">The rates from <see cref="GetRatesToBaseAsync"/>.</param>
    /// <param name="amount">The amount in <paramref name="currency"/>.</param>
    /// <param name="currency">ISO 4217 alpha code the amount is in; a code with no row is taken as already in the base.</param>
    /// <returns>The amount in the base currency.</returns>
    public static decimal ToBase(this IReadOnlyDictionary<string, decimal> ratesToBase, decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(ratesToBase);

        return amount * ratesToBase.GetValueOrDefault(currency, 1m);
    }

    /// <summary>Whether a code names a currency this panel is currently offering to clients.</summary>
    /// <remarks>
    /// "Offering" is both halves: configured, and switched on. A currency an operator set up and
    /// then disabled keeps the clients it already has (<c>Currency.Disable</c> says so) but may
    /// not be given to a new one, and neither may a code the panel has never heard of. Every
    /// place a client's currency is chosen, the admin form, guest checkout, asks this one
    /// question so that the three cannot drift apart on what "available" means.
    /// </remarks>
    /// <param name="context">The module's data context.</param>
    /// <param name="code">ISO 4217 alpha code, any case; looked up in the upper case it is stored in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the currency exists and is enabled.</returns>
    public static async Task<bool> IsCurrencyOfferedAsync(
        this IHostpanelDbContext context, string code, CancellationToken ct) =>
        await context.FindOfferedCurrencyAsync(code, ct) is not null;

    /// <summary>The currency a code names, when this panel is currently offering it to clients.</summary>
    /// <remarks>
    /// The same question as <see cref="IsCurrencyOfferedAsync"/>, answered with the row itself for
    /// a caller that goes on to bill in it and would otherwise look it up a second time.
    /// </remarks>
    /// <param name="context">The module's data context.</param>
    /// <param name="code">ISO 4217 alpha code, any case; looked up in the upper case it is stored in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The currency when it exists and is enabled; <see langword="null"/> otherwise.</returns>
    public static async Task<Currency?> FindOfferedCurrencyAsync(
        this IHostpanelDbContext context, string code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(code);

        var currency = await context.FindCurrencyAsync(code, ct);
        return currency is { IsEnabled: true } ? currency : null;
    }
}
