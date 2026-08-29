namespace Innovayse.API.RateLimiting.Options;

/// <summary>
/// One rate-limiting tier: how many requests a single caller may make in one window.
/// </summary>
/// <remarks>
/// <para>
/// A tier is a budget, not an endpoint. The same tier is reused by every route that shares a
/// reason for being limited, which is why nothing here is named after a controller: a policy
/// called <c>login</c> or <c>contact</c> is a number that can only ever be tuned for one place,
/// and the next endpoint with the same risk gets a second copy of it that drifts.
/// </para>
/// <para>
/// Carries no defaults of its own. A tier has no safe default in the abstract -- 5 and 1200 are
/// both correct depending on what the tier is for -- so the values are set on the properties of
/// <see cref="RateLimitOptions"/>, where the reason for each number is written down beside it.
/// </para>
/// </remarks>
public sealed class RateLimitTierOptions
{
    /// <summary>
    /// How many requests one caller may make inside <see cref="WindowSeconds"/> before being
    /// refused. Counted per partition -- per signed-in user, or per client address for callers
    /// with no credential.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// The length of the sliding window, in seconds. The window is divided into
    /// <see cref="RateLimitOptions.SegmentsPerWindow"/> segments, so the budget is released
    /// gradually rather than all at once on a boundary.
    /// </summary>
    public int WindowSeconds { get; set; }
}
