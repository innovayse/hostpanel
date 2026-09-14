namespace Innovayse.Domain.Products;

/// <summary>
/// Parses the billing-cycle strings the order flow and cart have always sent.
/// </summary>
/// <remarks>
/// Three spellings are in the wild — "monthly", "annual" and "annually" — because the client
/// and the seed data never agreed. They are accepted here, in one place, so no handler has to
/// carry its own switch over them.
/// </remarks>
public static class BillingCycleParser
{
    /// <summary>Parses a cycle string.</summary>
    /// <param name="raw">"monthly", "annual" or "annually", any case.</param>
    /// <returns>The cycle.</returns>
    /// <exception cref="ArgumentException">The string is none of the known spellings.</exception>
    public static BillingCycle Parse(string raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "monthly" => BillingCycle.Monthly,
        "annual" or "annually" => BillingCycle.Annual,
        _ => throw new ArgumentException($"Unknown billing cycle '{raw}'.", nameof(raw)),
    };
}
