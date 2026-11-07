using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;

/// <summary>The answer to "does this account exist, and what is it".</summary>
/// <param name="Outcome">Whether the account was found, is known to be absent, or is not known.</param>
/// <param name="Value">The account when <paramref name="Outcome"/> is <see cref="WhmLookupOutcome.Found"/>; otherwise null.</param>
public sealed record WhmLookup(WhmLookupOutcome Outcome, WhmAccount? Value)
{
    /// <summary>The answer for an account WHM positively does not hold.</summary>
    public static WhmLookup NotFound { get; } = new(WhmLookupOutcome.NotFound, null);

    /// <summary>The answer for a reply that said nothing usable.</summary>
    public static WhmLookup Unknown { get; } = new(WhmLookupOutcome.Unknown, null);

    /// <summary>The answer for a found account.</summary>
    /// <param name="account">The account.</param>
    /// <returns>A found lookup.</returns>
    public static WhmLookup Found(WhmAccount account) => new(WhmLookupOutcome.Found, account);
}
