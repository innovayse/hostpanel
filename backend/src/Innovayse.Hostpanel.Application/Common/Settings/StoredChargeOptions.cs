namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>
/// How the renewal run retries a stored card charge that failed. Bound from the "StoredCharges" section; a deployment
/// sets these only to change the defaults.
/// </summary>
/// <remarks>
/// A declined charge, or one the gateway confirmed it never made, is not tried again at once: the run waits
/// <see cref="RetryAfterDays"/> days after the last attempt, and stops after <see cref="MaxAttemptsPerInvoice"/>
/// attempts, leaving the invoice to the usual invoice and pay link flow. Without the wait every renewal pass would
/// charge a declined card again and again.
/// </remarks>
public sealed class StoredChargeOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "StoredCharges";

    /// <summary>The most days a deployment may ask the run to wait between two attempts.</summary>
    public const int MaxRetryAfterDaysLimit = 90;

    /// <summary>The most attempts per invoice a deployment may allow.</summary>
    public const int MaxAttemptsLimit = 20;

    /// <summary>Gets or sets the days to wait after a declined or abandoned attempt before the invoice is charged again.</summary>
    public int RetryAfterDays { get; set; } = 3;

    /// <summary>Gets or sets how many charge attempts one invoice gets from the renewal run; after that it is left unpaid for the usual flow.</summary>
    public int MaxAttemptsPerInvoice { get; set; } = 3;

    /// <summary>Gets whether the values are usable: a wait of 1 to <see cref="MaxRetryAfterDaysLimit"/> days and 1 to <see cref="MaxAttemptsLimit"/> attempts.</summary>
    public bool IsValid =>
        RetryAfterDays is >= 1 and <= MaxRetryAfterDaysLimit
        && MaxAttemptsPerInvoice is >= 1 and <= MaxAttemptsLimit;

    /// <summary>Gets the wait between two attempts as a time span.</summary>
    public TimeSpan RetryAfter => TimeSpan.FromDays(RetryAfterDays);
}
