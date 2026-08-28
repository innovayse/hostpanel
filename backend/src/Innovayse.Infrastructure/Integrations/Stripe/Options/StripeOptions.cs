namespace Innovayse.Infrastructure.Integrations.Stripe.Options;

/// <summary>
/// Configuration options for the Stripe payment integration.
/// Bound from the "Stripe" section in appsettings.
/// </summary>
/// <remarks>
/// Stripe is optional: a deployment that takes no card payments configures none of this, and the
/// Stripe service fails on the first call that actually needs Stripe rather than at startup. A
/// partly filled section is a different thing -- a misconfiguration -- and
/// <see cref="IsUsable"/> is what the composition root checks to refuse it.
/// </remarks>
public sealed class StripeOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Stripe";

    /// <summary>
    /// Stripe secret API key (the <c>sk_</c> credential). Empty means Stripe is not configured.
    /// Never carries a default: it is a secret, and a fabricated one fails against the live API
    /// with an error that names nothing about configuration.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable API key (the <c>pk_</c> credential) handed to the browser. Empty means
    /// Stripe is not configured.
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe webhook signing secret (the <c>whsec_</c> credential) used to verify inbound webhook
    /// payloads. Empty means webhook verification is not configured.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Whether the whole section was left unset. An untouched section is a deployment that does
    /// not take Stripe payments, which is allowed.
    /// </summary>
    public bool IsAbsent =>
        SecretKey.Length == 0 && PublishableKey.Length == 0 && WebhookSecret.Length == 0;

    /// <summary>
    /// Whether the one value the server side cannot work without is present. The publishable key
    /// and webhook secret are deliberately excluded: the first is only ever needed by the browser
    /// and the second only by a deployment that exposes a webhook endpoint, so requiring either
    /// here would refuse to start a deployment that charges cards perfectly well without them.
    /// </summary>
    public bool IsConfigured => SecretKey.Length > 0;

    /// <summary>
    /// Whether this section is in a state the process may start with -- either entirely unset, or
    /// filled in far enough to actually charge a card. Half-filled is neither.
    /// </summary>
    public bool IsUsable => IsAbsent || IsConfigured;
}
