using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>The answer to "does this object exist, and what is it".</summary>
/// <typeparam name="T">The object's details.</typeparam>
/// <param name="Outcome">Whether the object was found, is known to be absent, or is not known.</param>
/// <param name="Value">The details when <paramref name="Outcome"/> is <see cref="PleskLookupOutcome.Found"/>; otherwise null.</param>
public sealed record PleskLookup<T>(PleskLookupOutcome Outcome, T? Value)
    where T : class
{
    /// <summary>The answer for a found object.</summary>
    /// <param name="value">The object's details.</param>
    /// <returns>A found lookup.</returns>
    public static PleskLookup<T> Found(T value) => new(PleskLookupOutcome.Found, value);

    /// <summary>The answer for an object Plesk positively does not hold.</summary>
    public static PleskLookup<T> NotFound { get; } = new(PleskLookupOutcome.NotFound, null);

    /// <summary>The answer for a reply that said nothing usable.</summary>
    public static PleskLookup<T> Unknown { get; } = new(PleskLookupOutcome.Unknown, null);
}
