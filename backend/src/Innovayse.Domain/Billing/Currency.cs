namespace Innovayse.Domain.Billing;

using System.Globalization;
using Innovayse.Domain.Common;

/// <summary>
/// A currency this panel bills in. One row is the base; every other row carries a rate to it.
/// </summary>
/// <remarks>
/// Prices are never computed from the rate at checkout. The rate exists for one admin action,
/// "update product prices", which writes converted figures into <see cref="Products.ProductPrice"/>
/// rows the admin then edits. What a client sees and is charged is always a stored number.
/// </remarks>
public sealed class Currency : AggregateRoot
{
    /// <summary>Length of an ISO 4217 alpha or numeric code.</summary>
    private const int CodeLength = 3;

    /// <summary>ISO 4217 alpha code, upper case (e.g. "USD"). The identity of the row.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>ISO 4217 numeric code as a three-character string (e.g. "051"). What gateways speak.</summary>
    public string Numeric { get; private set; } = string.Empty;

    /// <summary>Text shown before the amount (e.g. "$"). May be empty.</summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>Text shown after the amount (e.g. " ֏"). May be empty.</summary>
    public string Suffix { get; private set; } = string.Empty;

    /// <summary>Decimal places this currency is shown and rounded to. AMD is conventionally 0.</summary>
    public int Decimals { get; private set; }

    /// <summary>How much of the base currency one unit of this currency is worth. The base has 1.</summary>
    public decimal RateToBase { get; private set; }

    /// <summary>Whether this is the base currency. Exactly one row is.</summary>
    public bool IsBase { get; private set; }

    /// <summary>Whether clients may be billed in it. A currency can be set up before it is offered.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private Currency() : base(0) { }

    /// <summary>Creates a currency.</summary>
    /// <param name="code">ISO 4217 alpha code; normalised to upper case.</param>
    /// <param name="numeric">ISO 4217 numeric code, three characters.</param>
    /// <param name="prefix">Text before the amount.</param>
    /// <param name="suffix">Text after the amount.</param>
    /// <param name="decimals">Decimal places.</param>
    /// <param name="rateToBase">Value of one unit in the base currency; must be 1 for the base.</param>
    /// <param name="isBase">Whether this is the base currency.</param>
    /// <returns>An enabled currency.</returns>
    /// <exception cref="ArgumentException">A code is not three characters, or the base's rate is not 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rate is not positive or decimals are negative.</exception>
    public static Currency Create(string code, string numeric, string prefix, string suffix, int decimals, decimal rateToBase, bool isBase)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(numeric);
        if (code.Length != CodeLength) throw new ArgumentException("An ISO 4217 alpha code is three letters.", nameof(code));
        if (numeric.Length != CodeLength) throw new ArgumentException("An ISO 4217 numeric code is three digits.", nameof(numeric));
        if (decimals < 0) throw new ArgumentOutOfRangeException(nameof(decimals));
        if (rateToBase <= 0) throw new ArgumentOutOfRangeException(nameof(rateToBase), "A rate must be positive.");
        if (isBase && rateToBase != 1m) throw new ArgumentException("The base currency's rate to itself is 1.", nameof(rateToBase));

        return new Currency
        {
            Code = code.ToUpperInvariant(),
            Numeric = numeric,
            Prefix = prefix ?? string.Empty,
            Suffix = suffix ?? string.Empty,
            Decimals = decimals,
            RateToBase = rateToBase,
            IsBase = isBase,
            IsEnabled = true,
        };
    }

    /// <summary>Formats an amount in this currency's own shape — decimals, prefix, suffix. Locale plays no part.</summary>
    /// <param name="amount">The amount in this currency.</param>
    /// <returns>E.g. "$2.99" or "1200 ֏".</returns>
    public string Format(decimal amount)
        => $"{Prefix}{Round(amount).ToString($"F{Decimals}", CultureInfo.InvariantCulture)}{Suffix}";

    /// <summary>Rounds to this currency's decimals, half away from zero.</summary>
    /// <param name="amount">The amount to round.</param>
    /// <returns>The rounded amount.</returns>
    public decimal Round(decimal amount) => Math.Round(amount, Decimals, MidpointRounding.AwayFromZero);

    /// <summary>Replaces the rate to base.</summary>
    /// <param name="rateToBase">The new rate; must be positive, and 1 when this is the base.</param>
    /// <exception cref="ArgumentOutOfRangeException">The rate is not positive.</exception>
    /// <exception cref="InvalidOperationException">This is the base and the rate is not 1.</exception>
    public void UpdateRate(decimal rateToBase)
    {
        if (rateToBase <= 0) throw new ArgumentOutOfRangeException(nameof(rateToBase), "A rate must be positive.");
        if (IsBase && rateToBase != 1m) throw new InvalidOperationException("The base currency's rate is always 1.");
        RateToBase = rateToBase;
    }

    /// <summary>Makes this the base currency; its rate becomes 1. The caller demotes the old base.</summary>
    public void MakeBase()
    {
        IsBase = true;
        RateToBase = 1m;
    }

    /// <summary>Stops this being the base. The caller promotes another row first.</summary>
    public void UnmakeBase() => IsBase = false;

    /// <summary>Allows clients to be billed in this currency.</summary>
    public void Enable() => IsEnabled = true;

    /// <summary>Stops offering this currency to new clients. Existing clients keep it.</summary>
    /// <exception cref="InvalidOperationException">The base currency cannot be disabled.</exception>
    public void Disable()
    {
        if (IsBase) throw new InvalidOperationException("The base currency cannot be disabled.");
        IsEnabled = false;
    }

    /// <summary>Updates the display fields.</summary>
    /// <param name="prefix">Text before the amount.</param>
    /// <param name="suffix">Text after the amount.</param>
    /// <param name="decimals">Decimal places; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Decimals are negative.</exception>
    public void UpdateDisplay(string prefix, string suffix, int decimals)
    {
        if (decimals < 0) throw new ArgumentOutOfRangeException(nameof(decimals));
        Prefix = prefix ?? string.Empty;
        Suffix = suffix ?? string.Empty;
        Decimals = decimals;
    }
}
