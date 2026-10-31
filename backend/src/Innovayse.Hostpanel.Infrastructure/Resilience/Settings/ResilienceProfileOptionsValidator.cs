using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Resilience.Settings;

/// <summary>
/// Checks the <see cref="TimeSpan"/> bounds of one <see cref="ResilienceProfileOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// These bounds used to be <c>[Range(typeof(TimeSpan), ...)]</c>. <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>
/// parses such string bounds lazily on first use and that set-up is not thread-safe, so several hosts
/// validating at once could throw <see cref="InvalidCastException"/>. Plain comparisons against
/// constants share no lazily built state and are deterministic.
/// </para>
/// <para>
/// Not registered in the container on its own: <see cref="HttpResilienceOptionsValidator"/> runs it for every
/// profile, because the profiles are nested and the options system only validates the section object.
/// </para>
/// </remarks>
internal sealed class ResilienceProfileOptionsValidator : IValidateOptions<ResilienceProfileOptions>
{
    /// <summary>The smallest accepted per-attempt or overall timeout.</summary>
    public static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>The largest accepted timeout, sampling window or break duration.</summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The largest accepted first retry delay.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>The smallest accepted sampling window or break duration.</summary>
    public static readonly TimeSpan MinWindow = TimeSpan.FromSeconds(1);

    /// <summary>Checks one profile.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The profile to check.</param>
    /// <returns>Success, or a failure listing every bound that is out of range.</returns>
    public ValidateOptionsResult Validate(string? name, ResilienceProfileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        Check(failures, nameof(ResilienceProfileOptions.AttemptTimeout), options.AttemptTimeout, MinTimeout, MaxTimeout);
        Check(failures, nameof(ResilienceProfileOptions.TotalTimeout), options.TotalTimeout, MinTimeout, MaxTimeout);
        Check(failures, nameof(ResilienceProfileOptions.RetryDelay), options.RetryDelay, TimeSpan.Zero, MaxRetryDelay);
        Check(failures, nameof(ResilienceProfileOptions.SamplingDuration), options.SamplingDuration, MinWindow, MaxTimeout);
        Check(failures, nameof(ResilienceProfileOptions.BreakDuration), options.BreakDuration, MinWindow, MaxTimeout);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Adds a failure when a value lies outside its inclusive bounds.</summary>
    /// <param name="failures">The failures collected so far.</param>
    /// <param name="property">The property name, for the message.</param>
    /// <param name="value">The value to check.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    private static void Check(List<string> failures, string property, TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min || value > max)
        {
            failures.Add($"The field {property} must be between {min} and {max}.");
        }
    }
}
