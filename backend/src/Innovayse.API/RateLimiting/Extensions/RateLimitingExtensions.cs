namespace Innovayse.API.RateLimiting.Extensions;

using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Innovayse.API.RateLimiting.Options;
using Innovayse.Application.Resources;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

/// <summary>
/// Registers this API's rate limiting: one global budget every request is measured against, four
/// named tiers an action may opt into on top of it, and the forwarded-header handling both depend
/// on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Global first, policies second.</b> The decisive choice here is that
/// <see cref="RateLimiterOptions.GlobalLimiter"/> is set, so nothing is unlimited by default. An
/// opt-in scheme protects only what somebody remembered to decorate, and this API has
/// fifty-eight controllers -- "remembered to decorate" is not a property that survives the next
/// controller anyone adds. The named policies tighten specific areas; they never turn limiting on.
/// </para>
/// <para>
/// <b>Both limiters apply, and the stricter one binds.</b> A request to an endpoint carrying a
/// policy spends from that policy's budget <i>and</i> from the global one. That is harmless while
/// every named tier is smaller than <see cref="RateLimitOptions.Global"/>, and silently wrong the
/// moment one is not: a tier meant to be more generous than the backstop would be capped by it,
/// with nothing to say so. Rather than exempt decorated endpoints from the global limiter -- which
/// would take the backstop away from precisely the routes that matter most -- the invariant is
/// checked at startup, so a tier raised above the global refuses to boot instead of quietly
/// having no effect.
/// </para>
/// <para>
/// <b>Partitioned by subject, then by address.</b> A signed-in caller is measured on their own
/// account. Only a caller with no credential falls back to an address, and that matters here
/// because a whole office behind one NAT, and every browser behind one corporate proxy, share an
/// address -- limiting on it alone lets one person's burst refuse their colleagues.
/// </para>
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>Key under which each partitioner records which policy it applied.</summary>
    /// <remarks>
    /// The rejection callback is handed a lease and an <c>HttpContext</c> and nothing that says
    /// which limiter refused, so the partitioner -- the one place that knows -- writes it down.
    /// Without it the log line and the <c>X-RateLimit-Policy</c> header cannot name the budget
    /// that was spent, and a limit nobody can attribute is a limit nobody can tune.
    /// </remarks>
    private const string PolicyItemKey = "Innovayse.RateLimit.Policy";

    /// <summary>The name recorded for the global budget, which is not an opt-in policy.</summary>
    private const string GlobalPolicyName = "global";

    /// <summary>The machine-readable code the 429 body carries.</summary>
    /// <remarks>
    /// SCREAMING_SNAKE, matching every other code <c>ExceptionMiddleware</c> writes, because the
    /// frontend's one mapping table (<c>client/utils/portalErrorMessages.ts</c>) branches on these
    /// strings. A rate-limited caller must be able to be told they are going too fast rather than
    /// shown the generic "something went wrong", which invites the retry the limit exists to stop.
    /// </remarks>
    private const string RateLimitedCode = "RATE_LIMITED";

    /// <summary>
    /// Resource key for the sentence the 429 body carries, in
    /// <c>Innovayse.Application/Resources/ValidationMessages*.resx</c>.
    /// </summary>
    /// <remarks>
    /// A key rather than a literal because the wording for every other refusal moved into that
    /// resource set, in the culture <c>UseRequestLocalization</c> read off <c>Accept-Language</c>.
    /// The portal ships en/ru/hy and no longer keeps a mapping table of its own, so an English
    /// sentence hard-coded here would be the one refusal a Russian or Armenian customer could not
    /// read -- on the response that most needs to be understood, since a caller who does not
    /// realise they are being asked to slow down retries straight back into the limit.
    /// </remarks>
    private const string RateLimitedMessageKey = "RateLimited";

    /// <summary>Logger category for the rejection warnings.</summary>
    private const string LoggerCategory = "Innovayse.API.RateLimiting";

    /// <summary>
    /// Paths that bypass rate limiting entirely, matched case-insensitively as segment prefixes.
    /// </summary>
    /// <remarks>
    /// Both liveness probes -- the minimal-API <c>/health</c> the container healthcheck calls and
    /// the <c>/api/health</c> controller the integration tests use. A probe runs every few seconds
    /// forever and always from the same address, and one that is refused is read by the restart
    /// policy as a dead process, so a limit applied here would eventually restart a healthy
    /// container. Nothing else is exempt: an endpoint that is cheap today is not a reason to leave
    /// it unmeasured.
    /// </remarks>
    private static readonly string[] ExemptPathPrefixes = ["/health", "/api/health"];

    /// <summary>
    /// Binds the <c>RateLimit</c> section and registers forwarded-header handling, the global
    /// limiter and the named policies.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">Application configuration, read here because this is the
    /// composition root -- the limiter is built once at startup from fixed numbers, so nothing
    /// below this point ever sees an <c>IConfiguration</c> or a settings key.</param>
    /// <param name="environment">The host environment, used to switch limiting off under
    /// <c>Testing</c>.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddPlatformRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Bound eagerly rather than through IOptions<T> because the limiter is constructed once,
        // at startup, from these numbers -- there is no consumer to inject them into. The
        // AddOptions registration below exists for the startup validation, which is what turns a
        // typo in appsettings into a refused boot instead of a budget silently at zero.
        var limits = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .Validate(
                o => IsPositive(o.Global) && IsPositive(o.Auth) && IsPositive(o.Upstream)
                    && IsPositive(o.Strict) && o.ConcurrentPermitLimit > 0 && o.TrustedProxyCount >= 0,
                $"Every {RateLimitOptions.SectionName} tier needs a positive PermitLimit and "
                    + "WindowSeconds, ConcurrentPermitLimit must be positive, and TrustedProxyCount "
                    + "must not be negative.")

            // The invariant the class documents. A tier raised above the global backstop would be
            // capped by it and appear to have been applied, which is the failure mode worth
            // refusing to start over rather than discovering from a support ticket.
            .Validate(
                o => o.Auth.PermitLimit <= o.Global.PermitLimit
                    && o.Upstream.PermitLimit <= o.Global.PermitLimit
                    && o.Strict.PermitLimit <= o.Global.PermitLimit,
                $"No {RateLimitOptions.SectionName} tier may allow more than Global, because the "
                    + "global budget applies to every request as well: the smaller of the two "
                    + "always binds, so a larger tier would silently have no effect.")
            .ValidateOnStart();

        // Forwarded-header handling is configured here, beside the proxy count it shares, rather
        // than as a second setting somewhere else that could disagree with it.
        //
        // KnownNetworks and KnownProxies are cleared because the chain arrives over Docker's
        // internal network with container addresses that are neither stable nor enumerable -- not
        // because the header is trusted. This container is not reachable except through nginx.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = limits.TrustedProxyCount;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Integration tests drive the same host and would otherwise spend a shared budget across a
        // whole run -- a suite that passes alone and fails when another one runs beside it, which
        // is the worst shape a test failure can take. The suites name this environment explicitly
        // (WebApplicationFactory ... UseEnvironment("Testing")), the same switch Program.cs
        // already uses for Serilog, migrations and seeding.
        //
        // NoLimiter rather than a limit of int.MaxValue: there is no counter to keep and no
        // partition dictionary to grow, so the behaviour is "off" rather than "improbably large".
        var limitingDisabled = environment.IsEnvironment("Testing");

        // Read back by the rejection callback so X-RateLimit-Limit can state the budget that was
        // actually spent. Built here because the callback has only a policy name to go on.
        var permitLimitsByPolicy = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [GlobalPolicyName] = limits.Global.PermitLimit,
            [RateLimitPolicies.Auth] = limits.Auth.PermitLimit,
            [RateLimitPolicies.Upstream] = limits.Upstream.PermitLimit,
            [RateLimitPolicies.Strict] = limits.Strict.PermitLimit,
            [RateLimitPolicies.Concurrent] = limits.ConcurrentPermitLimit,
        };

        services.AddRateLimiter(options =>
        {
            // 429, not the framework's default 503. The two mean opposite things to a caller: 503
            // says the server is unavailable and invites the retry that transport-level clients
            // perform automatically, while 429 says the caller is going too fast and is the only
            // one of the two a client can act on correctly.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, cancellationToken) =>
                OnRejectedAsync(context, permitLimitsByPolicy, cancellationToken);

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (limitingDisabled || IsExempt(context.Request.Path))
                {
                    return RateLimitPartition.GetNoLimiter(GlobalPolicyName);
                }

                return SlidingWindowPartition(context, GlobalPolicyName, limits.Global);
            });

            options.AddPolicy(
                RateLimitPolicies.Auth,
                context => limitingDisabled
                    ? RateLimitPartition.GetNoLimiter(RateLimitPolicies.Auth)
                    : SlidingWindowPartition(context, RateLimitPolicies.Auth, limits.Auth));

            options.AddPolicy(
                RateLimitPolicies.Upstream,
                context => limitingDisabled
                    ? RateLimitPartition.GetNoLimiter(RateLimitPolicies.Upstream)
                    : SlidingWindowPartition(context, RateLimitPolicies.Upstream, limits.Upstream));

            options.AddPolicy(
                RateLimitPolicies.Strict,
                context => limitingDisabled
                    ? RateLimitPartition.GetNoLimiter(RateLimitPolicies.Strict)
                    : SlidingWindowPartition(context, RateLimitPolicies.Strict, limits.Strict));

            options.AddPolicy(
                RateLimitPolicies.Concurrent,
                context =>
                {
                    if (limitingDisabled)
                    {
                        return RateLimitPartition.GetNoLimiter(RateLimitPolicies.Concurrent);
                    }

                    context.Items[PolicyItemKey] = RateLimitPolicies.Concurrent;

                    return RateLimitPartition.GetConcurrencyLimiter(
                        $"{RateLimitPolicies.Concurrent}|{ResolvePartitionKey(context)}",
                        _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = limits.ConcurrentPermitLimit,

                            // Nothing queues. A queued export holds its connection open while the
                            // ones ahead of it hold table scans in memory, so waiting turns one
                            // caller's burst into a slow request for everybody; refusing tells
                            // them to come back instead.
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        });
                });
        });

        return services;
    }

    /// <summary>Reports whether a tier carries usable numbers.</summary>
    /// <param name="tier">The tier to check.</param>
    /// <returns><see langword="true"/> when both of its values are positive.</returns>
    private static bool IsPositive(RateLimitTierOptions tier) =>
        tier.PermitLimit > 0 && tier.WindowSeconds > 0;

    /// <summary>
    /// Builds the sliding-window partition for one request under one tier, and records which
    /// policy it belongs to.
    /// </summary>
    /// <param name="context">The request being partitioned.</param>
    /// <param name="policyName">The policy this partition belongs to, used in the rejection
    /// headers and log line.</param>
    /// <param name="tier">The budget to apply.</param>
    /// <returns>The partition this request is counted in.</returns>
    private static RateLimitPartition<string> SlidingWindowPartition(
        HttpContext context, string policyName, RateLimitTierOptions tier)
    {
        context.Items[PolicyItemKey] = policyName;

        // The policy name is part of the key so that the global budget and a tighter tier applied
        // to the same request are readable apart in a dump. They are separate limiter instances,
        // so this is legibility rather than correctness.
        var key = $"{policyName}|{ResolvePartitionKey(context)}";

        return RateLimitPartition.GetSlidingWindowLimiter(
            key,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = tier.PermitLimit,
                Window = TimeSpan.FromSeconds(tier.WindowSeconds),
                SegmentsPerWindow = RateLimitOptions.SegmentsPerWindow,

                // Refused outright rather than held. A queue makes a rate limit into a latency
                // limit: the caller still gets served, just later, so an abusive client keeps its
                // throughput and merely occupies connections while doing it.
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });
    }

    /// <summary>
    /// Works out which bucket a request is counted in: the signed-in subject where there is one,
    /// otherwise the client address.
    /// </summary>
    /// <param name="context">The request being partitioned.</param>
    /// <returns>A partition key, prefixed <c>user:</c> or <c>ip:</c> so the two can never
    /// collide.</returns>
    /// <remarks>
    /// <para>
    /// Three claim types are tried because this API accepts three credentials. The SSO's bearer
    /// tokens are read with inbound claim mapping switched off, so the subject arrives as the raw
    /// <c>sub</c> claim rather than as <c>NameIdentifier</c>; locally issued tokens and the cookie
    /// session put it in one or the other depending on how the principal was built. Missing all
    /// three is treated as anonymous rather than as an error -- a caller whose identity cannot be
    /// read is exactly the caller who should be limited by address.
    /// </para>
    /// <para>
    /// This only works because <c>UseRateLimiter</c> runs <i>after</i> authentication. Moved above
    /// it, <c>context.User</c> is empty on every request, the <c>user:</c> branch becomes
    /// unreachable, and the limiter silently degrades to one bucket per address -- which for the
    /// client portal, whose Nuxt server calls this API from a single container, is one bucket for
    /// the entire customer base. Nothing fails; it simply stops protecting anyone.
    /// </para>
    /// </remarks>
    private static string ResolvePartitionKey(HttpContext context)
    {
        var subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value
            ?? context.User.Identity?.Name;

        return string.IsNullOrWhiteSpace(subject)
            ? $"ip:{ResolveClientAddress(context)}"
            : $"user:{subject}";
    }

    /// <summary>
    /// The address of whoever actually sent the request, as resolved through the proxy chain.
    /// </summary>
    /// <param name="context">The request to resolve for.</param>
    /// <returns>The client address, falling back to loopback when the connection carries
    /// none.</returns>
    /// <remarks>
    /// <para>
    /// <b>Read nothing else, and do not "improve" this.</b> The obvious implementation -- read
    /// <c>CF-Connecting-IP</c>, then the first entry of <c>X-Forwarded-For</c>, then
    /// <c>X-Real-IP</c> -- is a bypass, not a belt-and-braces. Every one of those is a header the
    /// caller writes. Proxies <i>append</i> to <c>X-Forwarded-For</c>, so whatever a client puts
    /// there stays at the left-hand end: reading the first entry lets any caller mint a fresh
    /// partition key on every request and never reach a limit at all, while the code reads as
    /// though it works. <c>CF-Connecting-IP</c> is worse -- no deployment of this product sits
    /// behind Cloudflare, so nothing in the chain strips a forged one.
    /// </para>
    /// <para>
    /// <c>Connection.RemoteIpAddress</c> is the correct source because <c>UseForwardedHeaders</c>
    /// has already run and resolved it from the <i>right-hand</i> end of the header, consuming
    /// exactly <see cref="RateLimitOptions.TrustedProxyCount"/> entries -- the ones the trusted
    /// proxies appended, which a client cannot write. A forged entry is simply left behind.
    /// </para>
    /// <para>
    /// Loopback rather than a shared "unknown" bucket when there is no connection address at all:
    /// a single named bucket for every unidentifiable caller is one an attacker can deliberately
    /// join in order to spend somebody else's budget.
    /// </para>
    /// </remarks>
    private static string ResolveClientAddress(HttpContext context) =>
        (context.Connection.RemoteIpAddress ?? IPAddress.Loopback).ToString();

    /// <summary>Reports whether a path bypasses rate limiting.</summary>
    /// <param name="path">The request path.</param>
    /// <returns><see langword="true"/> when the path is one of the liveness probes.</returns>
    private static bool IsExempt(PathString path)
    {
        foreach (var prefix in ExemptPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Answers a refused request: the RFC 6585 headers, a warning naming what was spent, and the
    /// platform's own error body.
    /// </summary>
    /// <param name="context">The rejection, carrying the lease that was refused.</param>
    /// <param name="permitLimitsByPolicy">Each policy's budget, for the
    /// <c>X-RateLimit-Limit</c> header.</param>
    /// <param name="cancellationToken">Cancellation token for writing the body.</param>
    /// <returns>A task that completes once the response has been written.</returns>
    /// <remarks>
    /// <para>
    /// <b>The body is exactly what <c>ExceptionMiddleware</c> writes</b> -- <c>error</c>, the
    /// sentence a person reads, resolved from the same <c>ValidationMessages</c> resource set in the
    /// same request culture, and <c>code</c>, the string the frontend branches on -- and
    /// nothing else. Everything a caller might want to know about the limit travels in the
    /// headers, where it is standard, rather than in extra body fields that would make 429 the
    /// one failure on this API with a shape of its own.
    /// </para>
    /// <para>
    /// Without a body at all the response is empty, and the client BFF's error reader falls back
    /// to its generic sentence -- so the visitor is told "something went wrong" and retries
    /// straight into the same wall, which is the one behaviour the limit exists to prevent.
    /// </para>
    /// </remarks>
    private static async ValueTask OnRejectedAsync(
        OnRejectedContext context,
        IReadOnlyDictionary<string, int> permitLimitsByPolicy,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        var policy = httpContext.Items.TryGetValue(PolicyItemKey, out var recorded)
            && recorded is string recordedName
                ? recordedName
                : GlobalPolicyName;

        // The window limiters report how long is left; the concurrency limiter has nothing to
        // report, because what frees a permit is another request finishing rather than a clock.
        // One second is the honest answer there -- come back shortly -- rather than a made-up
        // window that would have the caller wait longer than necessary.
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 1;

        var headers = httpContext.Response.Headers;
        headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        headers["X-RateLimit-Policy"] = policy;
        headers["X-RateLimit-Remaining"] = "0";
        headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow
            .AddSeconds(retryAfterSeconds)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        if (permitLimitsByPolicy.TryGetValue(policy, out var permitLimit))
        {
            headers["X-RateLimit-Limit"] = permitLimit.ToString(CultureInfo.InvariantCulture);
        }

        // Warning, not Information: a limit firing is either an attack or a budget set too low,
        // and both are things an operator has to see. The partition key is written out because
        // without it the line says only that something was refused, and the first question anyone
        // asks is whether it was one caller or everybody.
        httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory)
            .LogWarning(
                "Rate limit {Policy} refused {Method} {Path} for {PartitionKey}; retry after {RetryAfter}s.",
                policy,
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                ResolvePartitionKey(httpContext),
                retryAfterSeconds);

        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<ValidationMessages>>();

        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            new { error = localizer[RateLimitedMessageKey].Value, code = RateLimitedCode }, cancellationToken);
    }
}
