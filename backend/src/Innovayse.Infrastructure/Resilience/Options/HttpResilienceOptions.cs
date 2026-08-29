namespace Innovayse.Infrastructure.Resilience.Options;

/// <summary>
/// One resilience profile per outbound HTTP client this product registers. Bound from the
/// "HttpResilience" section in appsettings and validated at startup.
/// </summary>
/// <remarks>
/// <para>
/// Every property here carries a default that is the measured, intended value, so a deployment
/// that configures none of this section gets the profiles described below rather than nothing.
/// The section exists so an operator on a slow link, or in front of an unusually slow registrar,
/// can move one number without a rebuild — not because the defaults are guesses.
/// </para>
/// <para>
/// <b>Do not collapse these into a shared constant.</b> The numbers differ because the calls
/// differ, and the difference is the point: a WHOIS lookup a person is waiting on and a cPanel
/// account creation that legitimately takes most of a minute cannot share a budget without one
/// of them being wrong. The reason for each is on the property.
/// </para>
/// </remarks>
public sealed class HttpResilienceOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "HttpResilience";

    /// <summary>
    /// The SSO's service API — profile lookups by subject, by email, in batches and by page.
    /// Every operation on it is a GET that reads a person, so all of them are safe to repeat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>3 seconds an attempt, 10 seconds in total.</b> This runs inside request handling on an
    /// authenticated page in SSO mode, so its budget is what a person will sit through, not what
    /// the SSO might eventually manage. Three attempts of three seconds plus jittered backoff
    /// fits inside ten.
    /// </para>
    /// <para>
    /// <b>The breaker is deliberately hard to open: 90% of at least 20 calls in 30 seconds, and
    /// it reopens after 5.</b> The failure mode worth thinking about here is that an open breaker
    /// means nobody can be looked up, and in SSO mode that reads to a visitor as "nobody can sign
    /// in". The alternative — no breaker — is worse, and not by a little: during a real SSO
    /// outage every request would still spend its full three seconds holding a thread, so the
    /// SSO's outage becomes hostpanel's thread pool. A breaker converts that into an immediate
    /// failure that self-heals within one page refresh, and nothing that would have succeeded is
    /// lost, because the calls it sheds are calls that were failing anyway.
    /// </para>
    /// <para>
    /// The two numbers that make that trade safe are the ratio and the throughput. At 0.5 a
    /// single sick endpoint among several healthy ones could open the breaker and lock everyone
    /// out of the parts that still worked; at 0.9 over a minimum of 20 calls the breaker only
    /// opens when very nearly everything is failing, which is the only situation in which
    /// shedding is the right answer.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions SsoRead { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(3),
        TotalTimeout = TimeSpan.FromSeconds(10),
        MaxRetryAttempts = 3,
        RetryDelay = TimeSpan.FromSeconds(1),
        CircuitBreakerEnabled = true,
        FailureRatio = 0.9,
        MinimumThroughput = 20,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    /// The SSO's TOTP endpoints — enable, verify, disable. Registered with no retry stage at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No retries, ever.</b> All three operations are POSTs that change a person's second
    /// factor. <c>enable</c> issues a fresh secret, so a repeat hands back a different QR code
    /// from the one already on screen; <c>verify</c> and <c>disable</c> spend a one-time code,
    /// and a repeat is either rejected as a replay or, worse, silently accepted twice. None of
    /// that is resilience.
    /// </para>
    /// <para>
    /// <b>5 seconds an attempt, 6 in total.</b> A person is at a screen with a code that expires
    /// in thirty; there is no budget for waiting.
    /// </para>
    /// <para>
    /// The breaker is shaped like <see cref="SsoRead"/>'s and for the same reason. A wrong code
    /// answers 4xx, which the breaker does not count, so ordinary mistyping cannot open it.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions SsoTwoFactor { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(5),
        TotalTimeout = TimeSpan.FromSeconds(6),
        MaxRetryAttempts = 0,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerEnabled = true,
        FailureRatio = 0.9,
        MinimumThroughput = 20,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    /// WHM on the configured cPanel server. Registered with no retry stage at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No retries, and the HTTP method is no guide here.</b> WHM's JSON API v1 is addressed
    /// entirely over GET, so a method-based predicate would happily repeat <c>createacct</c>,
    /// <c>removeacct</c> and <c>passwd</c> — every one of the seven functions this client calls
    /// is a write dressed as a GET. <c>create_user_session</c> is the only one that reads like a
    /// lookup and it is not: it mints a login session. There is no read on this client for a
    /// predicate to let through, so there is no retry stage.
    /// </para>
    /// <para>
    /// <b>45 seconds an attempt, 55 in total.</b> Account creation on a loaded WHM box is
    /// genuinely slow — it builds a home directory, a mail store and a DNS zone — and the
    /// registration's existing 60-second <see cref="HttpClient.Timeout"/> stays the outer
    /// backstop. What changes is that a hung connection is now abandoned at 45 seconds rather
    /// than at WHM's leisure.
    /// </para>
    /// <para>
    /// The breaker is on because this client is bound to one server by
    /// <c>CPanelOptions.ApiUrl</c>, so opening it sheds calls to exactly the box that is failing.
    /// A long 30-second break: a WHM that is refusing work does not recover in five.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions CPanel { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(45),
        TotalTimeout = TimeSpan.FromSeconds(55),
        MaxRetryAttempts = 0,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerEnabled = true,
        FailureRatio = 0.5,
        MinimumThroughput = 5,
        SamplingDuration = TimeSpan.FromSeconds(60),
        BreakDuration = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// The Telegram Bot API, for contact-form enquiries. Registered with no retry stage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No retries.</b> <c>sendMessage</c> is a POST with no idempotency key, and a repeat
    /// after a lost response posts the enquiry to the operator's chat twice. The enquiry has
    /// already been delivered by mail by the time this runs, so a duplicate is a cost with no
    /// matching benefit.
    /// </para>
    /// <para>
    /// <b>8 seconds an attempt, 9 in total</b>, inside the registration's existing 10-second
    /// <see cref="HttpClient.Timeout"/>. Short on purpose, and the reason is unchanged from the
    /// comment that has always been on this registration: the call sits between a delivered
    /// enquiry and the visitor's answer, so a Telegram that is merely slow must not hold the
    /// response open.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions Telegram { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(8),
        TotalTimeout = TimeSpan.FromSeconds(9),
        MaxRetryAttempts = 0,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerEnabled = true,
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(60),
        BreakDuration = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// CWP control panels. Registered with no retry stage and no breaker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No retries.</b> Every CWP call is a form POST, and the operation is carried in the
    /// body's <c>action</c> field rather than in the method or the path: create, suspend,
    /// unsuspend and terminate all post to <c>/v1/account</c>, and so does the account listing
    /// behind the server-info screen. A predicate cannot separate the read from the writes
    /// without re-reading a consumed request body, and guessing wrong here re-creates a hosting
    /// account. The one genuinely safe endpoint, <c>/v1/version</c>, is a version string on an
    /// admin screen; a retry stage that exists only for it is machinery with no user.
    /// </para>
    /// <para>
    /// <b>No breaker.</b> This client addresses a different server on every call — the base URL
    /// is a per-call argument, not a base address — so a breaker would be shared across every
    /// CWP node the platform owns and one dead box would stop provisioning on all of them.
    /// </para>
    /// <para>
    /// <b>12 seconds an attempt, 14 in total</b>, inside the registration's existing 15-second
    /// <see cref="HttpClient.Timeout"/>, which was already tuned for this panel.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions Cwp { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(12),
        TotalTimeout = TimeSpan.FromSeconds(14),
        MaxRetryAttempts = 0,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerEnabled = false,
    };

    /// <summary>
    /// CWP7 control panels, typed client and the "Cwp7" named client the provisioning factory
    /// resolves per server. Registered with no retry stage and no breaker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No retries and no breaker, for the same two reasons as <see cref="Cwp"/>:</b> the
    /// operation lives in the POST body rather than in the method or path, and the server is a
    /// per-call argument so a breaker would be shared across every CWP7 node.
    /// </para>
    /// <para>
    /// <b>100 seconds an attempt, 115 in total</b>, inside the registration's existing two-minute
    /// <see cref="HttpClient.Timeout"/>. This is the longest budget in the file and it is not an
    /// oversight: CWP7 account creation on a busy node routinely runs past a minute, and cutting
    /// it short leaves a half-built account nobody knows about. The gain here is not a shorter
    /// wait, it is that a connection that has stalled entirely is now abandoned at all.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions Cwp7 { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(100),
        TotalTimeout = TimeSpan.FromSeconds(115),
        MaxRetryAttempts = 0,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerEnabled = false,
    };

    /// <summary>
    /// The "migration" client, which pulls records out of a foreign install. Every request may
    /// be repeated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Retries everything, despite every call being a POST.</b> This is the one client where
    /// the method is misleading in the safe direction: the source's protocol posts a signed
    /// payload and answers with data, and all three operations — ping, totals, and a page of
    /// records — read. Nothing on the far side is created or spent, so a repeat is free. Two
    /// retries, five seconds apart, because a foreign install mid-migration is exactly the kind
    /// of host that drops one connection in twenty.
    /// </para>
    /// <para>
    /// <b>60 seconds an attempt, 240 in total</b>, inside the registration's existing five-minute
    /// <see cref="HttpClient.Timeout"/>. A page of records from a large WHMCS install is a real
    /// query on someone else's database; this is a background pull worker, not a request a person
    /// is waiting on.
    /// </para>
    /// <para>
    /// <b>No breaker.</b> The source URL is a per-job argument, so the registration is shared
    /// across every install ever migrated from and a breaker opened by one of them would fail
    /// jobs against the others.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions Migration { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(60),
        TotalTimeout = TimeSpan.FromSeconds(240),
        MaxRetryAttempts = 2,
        RetryDelay = TimeSpan.FromSeconds(5),
        CircuitBreakerEnabled = false,
    };

    /// <summary>
    /// The Name.am registrar API. Retries only the requests that cannot register or renew
    /// anything — see the predicate in <c>HttpClientResilienceExtensions</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the client that most needs a predicate rather than a policy.</b> It posts to
    /// two very different things: <c>/client/domains/check</c>, a pure availability lookup a
    /// visitor is waiting on, and <c>/client/carts/purchase</c>, which registers, transfers or
    /// renews a domain and bills for it. Retrying the whole client because most of its traffic is
    /// reads would buy a second registration on a dropped response. GET and PUT are repeated —
    /// PUT here is a whole-resource update of nameservers, contacts or the registrar lock, which
    /// ends in the state a single call would have — and POST only for the two paths named in the
    /// predicate.
    /// </para>
    /// <para>
    /// <b>10 seconds an attempt, 25 in total</b>, inside the registration's existing 30-second
    /// <see cref="HttpClient.Timeout"/>. An availability check is on the ordering path with a
    /// person watching a spinner, and ten seconds is already past the point where the answer
    /// stops feeling like an answer.
    /// </para>
    /// <para>
    /// The breaker is on: one registrar, one endpoint, so opening it sheds calls to exactly the
    /// thing that is down. Twenty seconds is short enough that a registrar having a bad minute
    /// does not become hostpanel having a bad ten.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions NameAm { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(10),
        TotalTimeout = TimeSpan.FromSeconds(25),
        MaxRetryAttempts = 2,
        RetryDelay = TimeSpan.FromSeconds(1),
        CircuitBreakerEnabled = true,
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(60),
        BreakDuration = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// The Namecheap XML API. Retries only its read commands — see the predicate in
    /// <c>HttpClientResilienceExtensions</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every Namecheap call is a GET, so the method says nothing.</b> The operation is the
    /// <c>Command</c> query parameter, and it ranges from <c>namecheap.domains.check</c> to
    /// <c>namecheap.domains.create</c> and <c>namecheap.domains.renew</c> on the same URL and the
    /// same verb. The predicate reads that parameter and repeats only the lookups; anything it
    /// does not recognise is treated as a write and left alone.
    /// </para>
    /// <para>
    /// <b>10 seconds an attempt, 25 in total</b>, matching <see cref="NameAm"/> and inside the
    /// registration's existing 30-second <see cref="HttpClient.Timeout"/> — the same availability
    /// check on the same ordering path, so the same budget. The breaker is on for the same reason
    /// too: one registrar behind one endpoint.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions Namecheap { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(10),
        TotalTimeout = TimeSpan.FromSeconds(25),
        MaxRetryAttempts = 2,
        RetryDelay = TimeSpan.FromSeconds(1),
        CircuitBreakerEnabled = true,
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(60),
        BreakDuration = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// The factory's default, unnamed client. Registered with no retry stage and no breaker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nobody knows what this one calls, which is the whole argument.</b> It is what
    /// <c>IHttpClientFactory.CreateClient()</c> hands to <c>CpanelWhmApi</c> for per-server WHM
    /// calls and to any payment plugin that asks the factory for a client without naming one. A
    /// payment plugin's POST may well be a charge. An unknown request is treated as
    /// non-repeatable: guessing wrong in that direction takes someone's money twice.
    /// </para>
    /// <para>
    /// <b>No breaker</b>, because every caller of it addresses a different host.
    /// </para>
    /// <para>
    /// <b>20 seconds an attempt, 25 in total.</b> Generous, because the callers are unknown and
    /// one of them is per-server WHM; the point of the number is only that it is not 100.
    /// </para>
    /// </remarks>
    public ResilienceProfileOptions Default { get; set; } = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(20),
        TotalTimeout = TimeSpan.FromSeconds(25),
        MaxRetryAttempts = 0,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerEnabled = false,
    };

    /// <summary>
    /// Every profile paired with the configuration key it binds from, for the startup validator
    /// to walk. Kept here rather than in the validator so adding a profile above is one edit.
    /// </summary>
    /// <returns>Key-and-profile pairs in declaration order.</returns>
    public IEnumerable<(string Key, ResilienceProfileOptions Profile)> EnumerateProfiles()
    {
        yield return (nameof(SsoRead), SsoRead);
        yield return (nameof(SsoTwoFactor), SsoTwoFactor);
        yield return (nameof(CPanel), CPanel);
        yield return (nameof(Telegram), Telegram);
        yield return (nameof(Cwp), Cwp);
        yield return (nameof(Cwp7), Cwp7);
        yield return (nameof(Migration), Migration);
        yield return (nameof(NameAm), NameAm);
        yield return (nameof(Namecheap), Namecheap);
        yield return (nameof(Default), Default);
    }
}
