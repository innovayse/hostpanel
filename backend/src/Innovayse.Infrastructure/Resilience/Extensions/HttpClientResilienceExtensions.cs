namespace Innovayse.Infrastructure.Resilience.Extensions;

using System.Net;
using Innovayse.Infrastructure.Resilience.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

/// <summary>
/// The resilience pipeline this product puts in front of every outbound HTTP client: a total
/// budget, a per-attempt timeout, an optional circuit breaker, and retries for exactly the
/// requests a repeat cannot make worse.
/// </summary>
/// <remarks>
/// <para>
/// Eleven client registrations previously had none of this, so a third party that stopped
/// answering held a request thread until <see cref="HttpClient"/>'s 100-second default expired.
/// The per-attempt timeout is the part that matters most; retries are the part most easily got
/// wrong.
/// </para>
/// <para>
/// <b>There is no blanket profile and no blanket retry, on purpose.</b> These clients call a
/// registrar, two control panels, an identity provider, a chat bot and a foreign install, and
/// they disagree about almost everything — how long a call legitimately takes, whether a repeat
/// is free or bills someone, and whether the registration addresses one host or every server the
/// platform owns. So the caller names a profile from <see cref="HttpResilienceOptions"/> and
/// names, separately, which of its requests may be repeated. The default for the second is
/// "none": a client is opted into retries deliberately or not at all.
/// </para>
/// <para>
/// <b>The HTTP method is not a usable idempotency signal in this codebase</b>, which is why
/// there is no shared method-based predicate. WHM and Namecheap perform every operation,
/// including account creation and domain registration, over GET; CWP and CWP7 perform every
/// operation, including the read ones, over POST with the verb in a form field; and the
/// Inecobank payment gateway posts a status lookup, a payment registration and a refund to the
/// same host over the same verb. A predicate copied from a service whose API is RESTful would
/// repeat <c>createacct</c> here — or <c>refund.do</c>.
/// </para>
/// </remarks>
public static class HttpClientResilienceExtensions
{
    /// <summary>
    /// Namecheap commands are dotted names whose final segment says what the call does. Every
    /// read this product issues is named <c>get*</c> — <c>getInfo</c>, <c>getHosts</c>,
    /// <c>getList</c>, <c>getEPPCode</c>, <c>getEmailForwarding</c> — and the one exception is
    /// <c>check</c>.
    /// </summary>
    private const string NamecheapReadPrefix = "get";

    /// <summary>The Namecheap availability lookup, the only read not named <c>get*</c>.</summary>
    private const string NamecheapCheckCommand = "check";

    /// <summary>The query-string parameter carrying the Namecheap operation.</summary>
    private const string NamecheapCommandParameter = "Command";

    /// <summary>
    /// The only Name.am POST paths a repeat cannot make worse. <c>/client/domains/check</c> is a
    /// pure availability lookup, and <c>/auth/login</c> issues a bearer token — a second token
    /// costs nothing and supersedes the first. Everything else this client posts goes to
    /// <c>/client/carts/purchase</c>, which registers, transfers or renews a domain and bills for
    /// it, so the list is an allowlist rather than a denylist: a path added to the client later
    /// is not repeated until someone names it here.
    /// </summary>
    private static readonly string[] NameAmRepeatablePostPaths =
        ["/client/domains/check", "/auth/login"];

    /// <summary>
    /// The name the Inecobank payment plugin asks <see cref="IHttpClientFactory"/> for. It is
    /// that provider's plugin id, spelled here as a literal rather than referenced as
    /// <c>InecobankPaymentGateway.PluginId</c>: the provider is loaded reflectively out of
    /// <c>plugins/</c> and Infrastructure deliberately does not reference it, so the two ends of
    /// this string are matched by convention and nothing else. Change one and the plugin drops
    /// back to the factory's unnamed client without a word — and loses this profile with it.
    /// </summary>
    public const string InecobankClientName = "innovayse-inecobank";

    /// <summary>
    /// The one Inecobank endpoint a repeat cannot make worse: a pure read of an order's status.
    /// An allowlist of one rather than a denylist, so an endpoint added to that provider later
    /// counts as a write until somebody names it here. The cost of guessing wrong in the other
    /// direction is a second refund or a second charge.
    /// </summary>
    private const string InecobankReadEndpoint = "getOrderStatusExtended.do";

    /// <summary>
    /// Applies a resilience pipeline to a registered HTTP client.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <param name="selectProfile">Picks this client's profile out of the bound options.</param>
    /// <param name="isRetryable">
    /// Whether a given request may be repeated. <see langword="null"/> — the default — adds no
    /// retry stage at all, which is the right answer for any client whose every operation
    /// provisions, charges or spends something.
    /// </param>
    /// <returns>The same <paramref name="builder"/>, so a registration reads as one chain.</returns>
    /// <remarks>
    /// Stages are added outermost first:
    /// <list type="number">
    /// <item><description>
    /// <b>Total timeout.</b> The ceiling on the whole call including every retry and backoff.
    /// Without it a caller waits the sum of the attempts, which is the failure mode retries
    /// introduce.
    /// </description></item>
    /// <item><description>
    /// <b>Retry</b>, only when <paramref name="isRetryable"/> is supplied and the profile allows
    /// at least one attempt. Exponential with jitter — without jitter every replica that failed
    /// together retries together, and the recovering third party is knocked over by the
    /// synchronised wave rather than by the original load.
    /// </description></item>
    /// <item><description>
    /// <b>Circuit breaker</b>, only when the profile enables it. Inside the retry stage so the
    /// retries of one call are what feed it.
    /// </description></item>
    /// <item><description>
    /// <b>Per-attempt timeout.</b> Innermost, so a hung connection is abandoned rather than
    /// holding the whole budget.
    /// </description></item>
    /// </list>
    /// The client's own <see cref="HttpClient.Timeout"/> stays the outermost backstop and is
    /// always larger than the profile's total, so the pipeline decides and the backstop only
    /// catches a pipeline that was configured wrong.
    /// </remarks>
    public static IHttpClientBuilder AddResilience(
        this IHttpClientBuilder builder,
        Func<HttpResilienceOptions, ResilienceProfileOptions> selectProfile,
        Func<HttpRequestMessage, bool>? isRetryable = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(selectProfile);

        builder.AddResilienceHandler($"innovayse-{builder.Name}", (pipeline, context) =>
        {
            var profile = selectProfile(
                context.ServiceProvider.GetRequiredService<IOptions<HttpResilienceOptions>>().Value);

            pipeline.AddTimeout(profile.TotalTimeout);

            if (isRetryable is not null && profile.MaxRetryAttempts > 0)
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = profile.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = profile.RetryDelay,

                    // The request is read from the resilience context rather than from the
                    // response, because on the exception path there is no response to read it
                    // off -- and that is exactly the path where knowing what was sent matters.
                    ShouldHandle = args => ValueTask.FromResult(
                        IsTransient(args.Outcome)
                        && IsRepeatable(
                            args.Context.GetRequestMessage() ?? args.Outcome.Result?.RequestMessage,
                            isRetryable)),
                });
            }

            if (profile.CircuitBreakerEnabled)
            {
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = profile.FailureRatio,
                    SamplingDuration = profile.SamplingDuration,
                    MinimumThroughput = profile.MinimumThroughput,
                    BreakDuration = profile.BreakDuration,
                });
            }

            pipeline.AddTimeout(profile.AttemptTimeout);
        });

        // AddResilienceHandler answers with a pipeline builder, not the client builder. Handing
        // the client builder back is what lets a registration keep reading as one chain.
        return builder;
    }

    /// <summary>
    /// Applies a profile to a client on which <em>every</em> operation is a read, so any request
    /// may be repeated.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <param name="selectProfile">Picks this client's profile out of the bound options.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// Use this only where the claim has been checked against the client's full surface, not
    /// where it is merely true of most of it. It is correct for the SSO's service API (four GETs
    /// that read a person) and for the migration source (three POSTs that all read, because the
    /// pull protocol posts a signed payload and gets data back). It is <b>not</b> correct for
    /// either registrar, which mixes lookups and purchases on the same client.
    /// </remarks>
    public static IHttpClientBuilder AddReadOnlyResilience(
        this IHttpClientBuilder builder,
        Func<HttpResilienceOptions, ResilienceProfileOptions> selectProfile) =>
        builder.AddResilience(selectProfile, static _ => true);

    /// <summary>
    /// Applies a profile with no retry stage: a bounded attempt, a bounded total, and the
    /// profile's breaker if it has one.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <param name="selectProfile">Picks this client's profile out of the bound options.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// The right shape for anything that provisions, charges, or spends a one-time code, and for
    /// any client whose safe and unsafe operations cannot be told apart from the request. That
    /// covers the two-factor endpoints, WHM, both CWP panels, Telegram, and the factory's
    /// unnamed client. The timeout alone is most of the value: it is what stops one slow third
    /// party from holding request threads.
    /// </remarks>
    public static IHttpClientBuilder AddNoRetryResilience(
        this IHttpClientBuilder builder,
        Func<HttpResilienceOptions, ResilienceProfileOptions> selectProfile) =>
        builder.AddResilience(selectProfile, isRetryable: null);

    /// <summary>
    /// Applies the Name.am profile, repeating only the requests that cannot register, transfer,
    /// renew or bill.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static IHttpClientBuilder AddNameAmResilience(this IHttpClientBuilder builder) =>
        builder.AddResilience(o => o.NameAm, IsNameAmRepeatable);

    /// <summary>
    /// Applies the Namecheap profile, repeating only its read commands.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static IHttpClientBuilder AddNamecheapResilience(this IHttpClientBuilder builder) =>
        builder.AddResilience(o => o.Namecheap, IsNamecheapRepeatable);

    /// <summary>
    /// Applies the Inecobank profile, repeating only the order-status lookup — never the call
    /// that opens a payment session and never the one that refunds.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static IHttpClientBuilder AddInecobankResilience(this IHttpClientBuilder builder) =>
        builder.AddResilience(o => o.Inecobank, IsInecobankRepeatable);

    /// <summary>
    /// Whether an Inecobank request may be repeated.
    /// </summary>
    /// <param name="request">The request that failed.</param>
    /// <returns>True only for the extended order-status lookup.</returns>
    /// <remarks>
    /// Every call this gateway makes is a POST of form-urlencoded credentials to
    /// <c>{gateway}/payment/rest/{endpoint}</c>, so the verb separates nothing and the operation
    /// has to be read out of the final path segment. <c>register.do</c> opens a payment session
    /// against a merchant order number and <c>refund.do</c> returns money to a cardholder with no
    /// idempotency key of its own; only <c>getOrderStatusExtended.do</c> reads, and reading it
    /// twice costs nothing.
    /// </remarks>
    private static bool IsInecobankRepeatable(HttpRequestMessage request)
    {
        var path = PathOf(request);
        var lastSlash = path.LastIndexOf('/');
        var endpoint = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;

        return endpoint.Equals(InecobankReadEndpoint, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a Name.am request may be repeated.
    /// </summary>
    /// <param name="request">The request that failed.</param>
    /// <returns>True for GET, HEAD and PUT, and for POST only to an allowlisted path.</returns>
    /// <remarks>
    /// PUT is repeatable here because Name.am uses it the way HTTP defines it: every one of the
    /// five call sites replaces a domain's nameservers, contacts, lock or auto-renew flag with a
    /// whole new representation, so a second identical PUT ends in the state the first would
    /// have. POST is where the money is — <c>/client/carts/purchase</c> is registration, transfer
    /// and renewal alike — so it is refused except for the two paths in
    /// <see cref="NameAmRepeatablePostPaths"/>.
    /// </remarks>
    private static bool IsNameAmRepeatable(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Get
            || request.Method == HttpMethod.Head
            || request.Method == HttpMethod.Put)
        {
            return true;
        }

        if (request.Method != HttpMethod.Post) return false;

        var path = PathOf(request);
        return NameAmRepeatablePostPaths.Any(
            p => path.EndsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a Namecheap request may be repeated.
    /// </summary>
    /// <param name="request">The request that failed.</param>
    /// <returns>True only when the <c>Command</c> parameter names a read.</returns>
    /// <remarks>
    /// The verb is always GET and the URL is always the same endpoint, so the operation has to be
    /// read out of the query string. Unrecognised commands are refused rather than allowed: a
    /// command added to the provider later is a write until someone establishes otherwise, and
    /// the cost of being wrong in that direction is a second domain registration.
    /// </remarks>
    private static bool IsNamecheapRepeatable(HttpRequestMessage request)
    {
        var command = CommandOf(request);
        if (command.Length == 0) return false;

        // "namecheap.domains.dns.getHosts" -> "getHosts". The operation is the final segment;
        // the ones before it name the area, and "domains" appears in reads and writes alike.
        var lastDot = command.LastIndexOf('.');
        var operation = lastDot >= 0 ? command[(lastDot + 1)..] : command;

        return operation.StartsWith(NamecheapReadPrefix, StringComparison.OrdinalIgnoreCase)
            || operation.Equals(NamecheapCheckCommand, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the <c>Command</c> query parameter off a Namecheap request.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns>The command name, or an empty string when it cannot be read.</returns>
    /// <remarks>
    /// Parsed by hand rather than with <c>QueryHelpers</c>, which lives in ASP.NET Core and is
    /// not a dependency this layer has.
    /// </remarks>
    private static string CommandOf(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri is null || !uri.IsAbsoluteUri) return string.Empty;

        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) continue;

            if (pair.AsSpan(0, separator).Equals(NamecheapCommandParameter, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return string.Empty;
    }

    /// <summary>
    /// The path a request was sent to, in a form that is safe to match against whether the client
    /// built an absolute URL or a relative one over a base address.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns>The path without its query string, or an empty string when there is no URI.</returns>
    private static string PathOf(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri is null) return string.Empty;
        if (uri.IsAbsoluteUri) return uri.AbsolutePath;

        var original = uri.OriginalString;
        var query = original.IndexOf('?');
        return query >= 0 ? original[..query] : original;
    }

    /// <summary>
    /// Whether an attempt failed in a way a repeat could plausibly fix.
    /// </summary>
    /// <param name="outcome">What the attempt produced — a response, or the exception it threw.</param>
    /// <returns>True for 5xx, 408, and connection-level or timeout exceptions.</returns>
    /// <remarks>
    /// A 4xx other than 408 is the caller's own request being wrong; repeating it produces the
    /// same answer more expensively. 5xx and 408 are the server or the hop, which is what a retry
    /// is for.
    /// </remarks>
    private static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
            return outcome.Exception is HttpRequestException or TimeoutException;

        var response = outcome.Result;
        if (response is null) return false;

        return (int)response.StatusCode >= 500
            || response.StatusCode == HttpStatusCode.RequestTimeout;
    }

    /// <summary>
    /// Applies a client's own repeatability rule, treating an unknown request as unrepeatable.
    /// </summary>
    /// <param name="request">The request that was attempted, when it is known.</param>
    /// <param name="isRetryable">The client's rule.</param>
    /// <returns>False whenever the request could not be recovered from the context.</returns>
    /// <remarks>
    /// Guessing in the other direction is what sends a second POST, so an absent request is a
    /// refusal rather than a permission.
    /// </remarks>
    private static bool IsRepeatable(
        HttpRequestMessage? request,
        Func<HttpRequestMessage, bool> isRetryable) =>
        request is not null && isRetryable(request);
}
