namespace Innovayse.API.RateLimiting.Options;

/// <summary>
/// Every rate-limiting budget this API applies, bound from the <c>RateLimit</c> section.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the numbers look generous.</b> The client portal is a Nuxt server that calls this API
/// itself (<c>client/server/utils/api.ts</c>, <c>internalApiCall</c>), so every request it relays
/// arrives from one container address. Authenticated portal traffic still partitions correctly,
/// because it carries the visitor's bearer token and the partition key prefers the subject over
/// the address -- but <i>anonymous</i> portal traffic (the marketing pages, the domain search, the
/// contact form) collapses into a single bucket shared by the entire public site. Anything sized
/// for one person would refuse real visitors there.
/// </para>
/// <para>
/// That does not make the limits pointless: a caller abusing an anonymous endpoint reaches it
/// through nginx as a browser would, and <i>that</i> path partitions on the real client address.
/// The tight tiers therefore bind on the attacker and not on the shared bucket, which is the
/// arrangement each number below is chosen for. The clean fix -- having the portal forward the
/// visitor's address so its bucket splits too -- is a change to the frontend and to nginx's header
/// handling, and is deliberately not made here.
/// </para>
/// </remarks>
public sealed class RateLimitOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "RateLimit";

    /// <summary>
    /// How many segments each sliding window is divided into. Six means a sixth of the budget is
    /// released every ten seconds on a sixty-second window.
    /// </summary>
    /// <remarks>
    /// Not configurable, and sliding rather than fixed on purpose. A fixed window lets a caller
    /// spend the whole budget in its last second and the whole of the next one immediately after,
    /// so the real short-term rate is twice the configured number -- which is exactly the burst
    /// these limits exist to flatten.
    /// </remarks>
    public const int SegmentsPerWindow = 6;

    /// <summary>
    /// The budget every request is measured against, whether or not its endpoint names a tighter
    /// tier. 1200 requests a minute -- twenty a second.
    /// </summary>
    /// <remarks>
    /// This is a ceiling on abuse, not a fairness quota. The binding constraint is the shared
    /// anonymous bucket described on this class: a public portal page fans out to roughly half a
    /// dozen backend calls, so 1200 a minute leaves room for around two hundred page views a
    /// minute from the whole site before a real visitor is refused. Against a scripted caller it
    /// still caps sustained traffic at twenty requests a second, two orders of magnitude below
    /// what an unthrottled loop manages, and a signed-in caller is measured on their own subject
    /// rather than on this bucket.
    /// </remarks>
    public RateLimitTierOptions Global { get; set; } = new() { PermitLimit = 1200, WindowSeconds = 60 };

    /// <summary>
    /// The budget for endpoints that accept a credential, a one-time code or a reset token --
    /// anything where a high request rate means guessing. 15 requests a minute.
    /// </summary>
    /// <remarks>
    /// Not 5. A person who mistypes a password twice and then resets it spends five requests in
    /// one minute on the happy path (two failed sign-ins, request the reset, confirm the mail,
    /// set the new password), and a second factor adds another; 5 would refuse the recovery flow
    /// the failed sign-in sent them to. Not 60 either: 15 a minute is 21,600 guesses a day, which
    /// is nothing against a password of any real entropy and nothing against a rotating six-digit
    /// TOTP code, while an unlimited endpoint is millions.
    /// </remarks>
    public RateLimitTierOptions Auth { get; set; } = new() { PermitLimit = 15, WindowSeconds = 60 };

    /// <summary>
    /// The budget for endpoints where every request costs a call to somebody else's system -- a
    /// domain registrar, a WHOIS server. 60 requests a minute.
    /// </summary>
    /// <remarks>
    /// The cost being protected is not this server's; it is a third party's quota, and in the
    /// registrar's case an account that can be throttled or billed. A visitor typing domain names
    /// by hand manages perhaps ten a minute, so 60 leaves the public site's shared bucket room for
    /// several people searching at once, while capping a direct scripted caller at one registrar
    /// call a second instead of as many as the connection allows.
    /// </remarks>
    public RateLimitTierOptions Upstream { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>
    /// The budget for anonymous writes that cause something to be delivered in the real world --
    /// mail leaving the relay, a message posted to the operator's chat. 5 requests a minute.
    /// </summary>
    /// <remarks>
    /// The one tier deliberately sized below what the shared anonymous bucket could theoretically
    /// need, because nobody submits an enquiry form five times a minute and a site that genuinely
    /// receives five enquiries in the same minute is not a shape worth optimising for. The reason
    /// it is this tight is that the damage is not load: a flood here fills an operator's inbox and
    /// chat, and can burn a relay's reputation, long before it troubles the server.
    /// </remarks>
    public RateLimitTierOptions Strict { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>
    /// How many expensive operations one caller may have in flight at once. Five.
    /// </summary>
    /// <remarks>
    /// A concurrency limit rather than a rate, because for a report export the cost is a table
    /// scan held in memory for the length of the request, not the number of requests -- ten
    /// exports spread over a minute are fine and five at the same instant are not. Five rather
    /// than one: an administrator opening several exports in tabs is ordinary use, and a limit of
    /// one would make the second tab fail rather than wait.
    /// </remarks>
    public int ConcurrentPermitLimit { get; set; } = 5;

    /// <summary>
    /// How many proxies stand between a visitor's browser and this process. Two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bound straight onto <c>ForwardedHeadersOptions.ForwardLimit</c>, which is what makes
    /// <c>Connection.RemoteIpAddress</c> the visitor's address rather than a proxy's. The deployed
    /// chain is browser -> edge proxy -> this stack's nginx -> Kestrel: nginx records the edge in
    /// <c>X-Forwarded-For</c> and the edge recorded the browser, so exactly two entries have to be
    /// consumed from the right-hand end before the address is the visitor's.
    /// </para>
    /// <para>
    /// Left at the framework default of 1 the middleware stops one entry short and every visitor
    /// resolves to the edge proxy -- one partition for the entire internet, which is the failure
    /// that looks most like success: the limiter runs, the counters move, and one person's burst
    /// refuses everybody.
    /// </para>
    /// <para>
    /// It is consumed from the right and never from the left, and that is the whole security
    /// property. Proxies append, so anything a caller forges stays at the left-hand end and is
    /// simply left behind; an implementation that read the first entry -- or <c>X-Real-IP</c>, or
    /// <c>CF-Connecting-IP</c>, none of which anything in this chain strips -- would let a caller
    /// mint a new partition per request and never reach a limit at all.
    /// </para>
    /// <para>
    /// The number has to match the deployment: a stack reached through one proxy sets this to 1,
    /// and a process reached directly sets it to 0. Setting it higher than the real chain is the
    /// dangerous direction, because the middleware would then consume an entry the caller wrote.
    /// </para>
    /// </remarks>
    public int TrustedProxyCount { get; set; } = 2;
}
