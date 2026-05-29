namespace Innovayse.Infrastructure.Billing;

/// <summary>Strongly typed Stripe configuration bound from appsettings.</summary>
public sealed class StripeSettings
{
    /// <summary>Gets or sets the Stripe secret API key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Stripe publishable API key.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Stripe webhook signing secret.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
