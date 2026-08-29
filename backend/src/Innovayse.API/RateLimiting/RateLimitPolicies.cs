namespace Innovayse.API.RateLimiting;

/// <summary>
/// The names of the rate-limiting policies an action may opt into with
/// <c>[EnableRateLimiting]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Constants rather than literals because the attribute takes a string and a policy that was
/// never registered does not fail to compile -- it throws at first request, on whichever route
/// nobody exercised before deploying.
/// </para>
/// <para>
/// <b>Every name here describes a reason, not a route.</b> There is no <c>login</c> policy and no
/// <c>contact</c> policy, deliberately: a budget named after one endpoint can only be tuned for
/// that endpoint, and the next route with the same risk profile ends up with a near-duplicate
/// policy that drifts from it. Naming the reason means the number is argued once and reused.
/// </para>
/// <para>
/// None of these is the default. Everything is limited by the global limiter whether or not it
/// carries an attribute -- see <c>RateLimitingExtensions</c> for why that is the load-bearing
/// part on an API with fifty-eight controllers.
/// </para>
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>
    /// Endpoints that accept a credential, a one-time code or a reset token. A high request rate
    /// against one of these means guessing, so the budget is small enough to make that pointless
    /// and large enough for a person who mistypes and then recovers.
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// Endpoints where serving one request costs a call to a third-party system -- the domain
    /// registrar, a WHOIS server. The budget protects somebody else's quota and bill, not this
    /// server's CPU.
    /// </summary>
    public const string Upstream = "upstream";

    /// <summary>
    /// Anonymous writes that deliver something outside the process: mail through the relay, a
    /// message into the operator's chat. The damage from a flood is an unusable inbox and a
    /// burnt sender reputation, which arrive long before any load problem does.
    /// </summary>
    public const string Strict = "strict";

    /// <summary>
    /// Operations whose cost is measured in what they hold rather than how often they are asked
    /// for -- report exports, a migration run. Limited by how many may be in flight at once,
    /// because a per-minute budget does not describe the harm.
    /// </summary>
    public const string Concurrent = "concurrent";
}
