namespace Innovayse.Hostpanel.Shared.Settings;

/// <summary>
/// Every rate-limiting budget this API applies, bound from the <c>RateLimit</c> section.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the numbers look generous.</b> The client portal is a Nuxt server that calls this API
/// itself (<c>client/server/utils/api.ts</c>, <c>internalApiCall</c>), so every request it relays
/// arrives from one container address. Authenticated portal traffic still partitions correctly,
/// because it carries the visitor's bearer token and the partition key prefers the subject over
/// the address, but <i>anonymous</i> portal traffic (the marketing pages, the domain search, the
/// contact form) collapses into a single bucket shared by the entire public site. Anything sized
/// for one person would refuse real visitors there.
/// </para>
/// <para>
/// That does not make the limits pointless: a caller abusing an anonymous endpoint reaches it
/// through nginx as a browser would, and <i>that</i> path partitions on the real client address.
/// The tight tiers therefore bind on the attacker and not on the shared bucket, which is the
/// arrangement each number below is chosen for. The clean fix, having the portal forward the
/// visitor's address so its bucket splits too, is a change to the frontend and to nginx's header
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
    /// so the real short-term rate is twice the configured number, which is exactly the burst
    /// these limits exist to flatten.
    /// </remarks>
    public const int SegmentsPerWindow = 6;

    /// <summary>
    /// The budget every request is measured against, whether or not its endpoint names a tighter
    /// tier. 1200 requests a minute: twenty a second.
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
    /// The budget for endpoints that accept a credential, a one-time code or a reset token:
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
    /// The budget for endpoints where every request costs a call to somebody else's system: a
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
    /// The budget for anonymous writes that cause something to be delivered in the real world:
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
    /// Gets or sets the tier for <see cref="Constants.RateLimitPolicies.SshKeys"/>: adding and
    /// revoking SSH keys.
    /// </summary>
    /// <remarks>
    /// Twenty a minute is far beyond what a person pasting keys does, and far below what would let
    /// one account keep a managed server's key agent busy: every permit is an SSH connection.
    /// </remarks>
    public RateLimitTierOptions SshKeys { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>
    /// How many expensive operations one caller may have in flight at once. Five.
    /// </summary>
    /// <remarks>
    /// A concurrency limit rather than a rate, because for a report export the cost is a table
    /// scan held in memory for the length of the request, not the number of requests: ten
    /// exports spread over a minute are fine and five at the same instant are not. Five rather
    /// than one: an administrator opening several exports in tabs is ordinary use, and a limit of
    /// one would make the second tab fail rather than wait.
    /// </remarks>
    public int ConcurrentPermitLimit { get; set; } = 5;

    /// <summary>
    /// Gets or sets the tier for <see cref="Constants.RateLimitPolicies.PluginDefault"/>: a plugin route that declares no
    /// tier. 120 requests a minute for one plugin and one caller.
    /// </summary>
    /// <remarks>
    /// Counted per plugin and per caller (the client account on a client call, the administrator on an admin call), so one
    /// plugin or one account cannot spend another's budget. Two a second is far above a person using a plugin's screens and
    /// far below what a script manages, and the plugin's own work, not this server's, is what it protects.
    /// </remarks>
    public RateLimitTierOptions PluginDefault { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    /// <summary>
    /// Gets or sets the tier for <see cref="Constants.RateLimitPolicies.PluginStrict"/>: a plugin route that is costly or
    /// sensitive, such as one that sends messages. 20 requests a minute for one plugin and one caller.
    /// </summary>
    public RateLimitTierOptions PluginStrict { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>
    /// Gets or sets the tier for <see cref="Constants.RateLimitPolicies.PluginHeavy"/>: a plugin route that does heavy work
    /// and is called rarely, such as a report or an export. 6 requests a minute for one plugin and one caller.
    /// </summary>
    public RateLimitTierOptions PluginHeavy { get; set; } = new() { PermitLimit = 6, WindowSeconds = 60 };

    /// <summary>
    /// How many streaming plugin routes one caller may hold open at once, per plugin. Three.
    /// </summary>
    /// <remarks>
    /// A concurrency limit like <see cref="ConcurrentPermitLimit"/>, because an open stream holds a connection and the
    /// plugin's work for as long as it lasts: it is not a rate. Three rather than one, so a person with a few tabs open
    /// on one plugin is not refused, and rather than five so that one account cannot hold a plugin's streams for everybody.
    /// </remarks>
    public int PluginStreamPermitLimit { get; set; } = 3;

    /// <summary>
    /// How many trusted proxies in a row stand between a visitor's browser and this process. Zero
    /// by default, so no forwarded header is believed until the deployment says which proxies it has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bound straight onto <c>ForwardedHeadersOptions.ForwardLimit</c>. <c>X-Forwarded-For</c> is read
    /// from the right-hand end, one entry per trusted hop, and only while the hop it came from is in
    /// <see cref="KnownProxies"/> or <see cref="KnownNetworks"/>; anything a caller forged stays at the
    /// left-hand end and is left behind.
    /// </para>
    /// <para>
    /// Setting it higher than the real chain is the dangerous direction, because the middleware would
    /// then consume an entry the caller wrote.
    /// </para>
    /// </remarks>
    public int TrustedProxyCount { get; set; }

    /// <summary>
    /// Addresses of the proxies whose forwarded headers are believed (<c>RateLimit__KnownProxies__N</c>).
    /// Each must parse as an IP address, or startup is refused.
    /// </summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// CIDR ranges of the proxies whose forwarded headers are believed (<c>RateLimit__KnownNetworks__N</c>),
    /// such as the Docker network the edge and client containers share. Each must parse as a range, or
    /// startup is refused.
    /// </summary>
    public List<string> KnownNetworks { get; set; } = [];
}
