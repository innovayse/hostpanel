namespace Innovayse.Infrastructure.Resilience.Options;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

/// <summary>
/// Startup validation for <see cref="HttpResilienceOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// Written by hand rather than left to <c>ValidateDataAnnotations()</c> because that only reads
/// attributes on the options object's own properties and never descends into the eleven nested
/// profiles — which is where every number actually lives. A misconfigured section would have
/// passed validation and then thrown out of <c>AddResilienceHandler</c> on the first call to the
/// affected client, in a different process lifetime and with a message naming Polly rather than
/// the setting.
/// </para>
/// <para>
/// It also checks the one rule no attribute can express: an attempt may not be allowed to take
/// longer than the whole call. Inverted that way the outer timeout fires first, every attempt is
/// cut short by it, and the per-attempt budget is silently dead.
/// </para>
/// </remarks>
public sealed class HttpResilienceOptionsValidator : IValidateOptions<HttpResilienceOptions>
{
    /// <summary>
    /// Validates every profile on the bound options.
    /// </summary>
    /// <param name="name">The named options instance; unused, only the default is registered.</param>
    /// <param name="options">The bound options to check.</param>
    /// <returns>
    /// <see cref="ValidateOptionsResult.Success"/>, or a failure listing every problem found —
    /// all of them at once, so a wrong section is fixed in one pass rather than one restart per
    /// mistake.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, HttpResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        foreach (var (key, profile) in options.EnumerateProfiles())
        {
            var prefix = $"{HttpResilienceOptions.SectionName}:{key}";
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(profile, new ValidationContext(profile), results, validateAllProperties: true))
            {
                failures.AddRange(results.Select(r => $"{prefix}: {r.ErrorMessage}"));
            }

            if (profile.TotalTimeout < profile.AttemptTimeout)
            {
                failures.Add(
                    $"{prefix}: TotalTimeout ({profile.TotalTimeout}) must be at least AttemptTimeout "
                    + $"({profile.AttemptTimeout}), or the outer timeout fires first and the "
                    + "per-attempt budget never applies.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
