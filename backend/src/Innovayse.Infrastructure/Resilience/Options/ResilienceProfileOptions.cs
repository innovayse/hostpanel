namespace Innovayse.Infrastructure.Resilience.Options;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The numbers one outbound HTTP client's resilience pipeline is built from: a per-attempt
/// timeout, a ceiling on the whole call, an optional retry ladder and an optional circuit
/// breaker.
/// </summary>
/// <remarks>
/// <para>
/// Every client gets its own instance of this rather than sharing one. A WHOIS lookup and a
/// cPanel account creation are not the same call and must not answer to the same budget: the
/// first is a page a person is waiting on, the second is a provisioning step that legitimately
/// takes the best part of a minute. The reason for each client's numbers is written on that
/// client's property in <see cref="HttpResilienceOptions"/>, next to the number itself, so a
/// later reader can see what it was measured against before changing it.
/// </para>
/// <para>
/// <b>Whether a retry happens at all is not decided here.</b> This class says how many times and
/// how far apart; <em>which requests may be repeated</em> is a per-client predicate in
/// <c>HttpClientResilienceExtensions</c>, because it depends on what the request does rather
/// than on a number an operator may tune. A client whose operations are all unsafe to repeat is
/// registered without a retry stage, and <see cref="MaxRetryAttempts"/> is then unused — raising
/// it in configuration will not make that client retry.
/// </para>
/// </remarks>
public sealed class ResilienceProfileOptions
{
    /// <summary>
    /// How long a single attempt may take before it is abandoned. This is the number that
    /// replaces <see cref="HttpClient"/>'s 100-second default, which is the real hazard being
    /// fixed here: a stalled third party otherwise holds a request thread for a minute and a
    /// half before anybody is told.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.1", "00:10:00")]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The ceiling on the whole call, retries and backoff delays included. Without it a caller
    /// waits the sum of the attempts, which is the failure mode retries introduce. Must be at
    /// least as large as <see cref="AttemptTimeout"/>, and must stay below the owning
    /// <see cref="HttpClient"/>'s own <see cref="HttpClient.Timeout"/>, which remains the
    /// outermost backstop.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.1", "00:10:00")]
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many times a retryable request is repeated after the first attempt. Ignored on a
    /// client registered without a retry stage — see the remarks on this class.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// The first backoff delay; subsequent ones grow exponentially from it. Jitter is always
    /// applied, because without it every replica that failed together retries together and the
    /// recovering third party is knocked over by the synchronised wave rather than by the
    /// original load.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether this client gets a circuit breaker at all.
    /// </summary>
    /// <remarks>
    /// Off for any client that addresses more than one host through the same registration. A
    /// breaker is per-registration, not per-host, so on a multi-target client one dead server
    /// would open the breaker for every healthy one — turning a single unreachable node into a
    /// platform-wide provisioning outage, which is a worse failure than the one the breaker
    /// exists to prevent.
    /// </remarks>
    public bool CircuitBreakerEnabled { get; set; } = true;

    /// <summary>
    /// The share of calls in <see cref="SamplingDuration"/> that must fail before the breaker
    /// opens, between 0 and 1 exclusive of 0.
    /// </summary>
    [Range(0.05, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// The fewest calls that must be seen in <see cref="SamplingDuration"/> before the ratio is
    /// consulted. This is what stops two failures in a quiet minute from opening the breaker.
    /// </summary>
    [Range(2, 1000)]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>The window the failure ratio is measured over.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long the breaker stays open before it lets a probe through. Short breaks favour
    /// recovering quickly over shedding load for long.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
