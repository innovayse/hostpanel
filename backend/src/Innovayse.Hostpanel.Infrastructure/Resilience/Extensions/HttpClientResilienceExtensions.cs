using System.Net;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;

/// <summary>
/// The resilience pipeline this product puts in front of every outbound HTTP client: a total
/// budget, a per-attempt timeout, an optional circuit breaker, and retries for exactly the
/// requests a repeat cannot make worse.
/// </summary>
/// <remarks>
/// <para>
/// Without it a third party that stopped answering would hold a request thread until
/// <see cref="HttpClient"/>'s 100-second default expired. The per-attempt timeout is the part
/// that matters most; retries are the part most easily got
/// wrong.
/// </para>
/// <para>
/// <b>There is no blanket profile and no blanket retry, on purpose.</b> These clients call a
/// registrar, two control panels, an identity provider, a chat bot and a foreign install, and
/// they disagree about almost everything: how long a call legitimately takes, whether a repeat
/// is free or bills someone, and whether the registration addresses one host or every server the
/// platform owns. So the caller names a profile from <see cref="HttpResilienceOptions"/> and
/// names, separately, which of its requests may be repeated. The default for the second is
/// "none": a client is opted into retries deliberately or not at all.
/// </para>
/// <para>
/// <b>The HTTP method is not a usable idempotency signal in this codebase</b>, which is why
/// there is no shared method-based predicate. WHM performs every operation,
/// including account creation, over GET; CWP performs every
/// operation, including the read ones, over POST with the verb in a form field; and the
/// payment gateways post a status lookup, a payment registration and a refund to the
/// same host over the same verb. A predicate copied from a service whose API is RESTful would
/// repeat <c>createacct</c> here, or <c>refund.do</c>.
/// </para>
/// </remarks>
public static class HttpClientResilienceExtensions
{
    /// <summary>
    /// Applies a resilience pipeline to a registered HTTP client.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <param name="selectProfile">Picks this client's profile out of the bound options.</param>
    /// <param name="isRetryable">
    /// Whether a given request may be repeated. <see langword="null"/>, the default, adds no
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
    /// at least one attempt. Exponential with jitter: without jitter every replica that failed
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
                    // off, and that is exactly the path where knowing what was sent matters.
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
    /// where it is merely true of most of it. It is correct for the sign-on provider's service API (four GETs
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
    /// Applies the generic plugin profile: a timeout and no retry, because a host cannot know
    /// which of a plugin's requests are reads. A plugin that needs retries on its reads owns them.
    /// </summary>
    /// <param name="builder">The client registration to wrap.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static IHttpClientBuilder AddPluginResilience(this IHttpClientBuilder builder) =>
        builder.AddNoRetryResilience(o => o.Plugin);

    /// <summary>
    /// Whether an attempt failed in a way a repeat could plausibly fix.
    /// </summary>
    /// <param name="outcome">What the attempt produced: a response, or the exception it threw.</param>
    /// <returns>True for 5xx, 408, and connection-level or timeout exceptions.</returns>
    /// <remarks>
    /// A 4xx other than 408 is the caller's own request being wrong; repeating it produces the
    /// same answer more expensively. 5xx and 408 are the server or the hop, which is what a retry
    /// is for.
    /// </remarks>
    private static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return outcome.Exception is HttpRequestException or TimeoutException;
        }

        var response = outcome.Result;
        if (response is null)
        {
            return false;
        }

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
