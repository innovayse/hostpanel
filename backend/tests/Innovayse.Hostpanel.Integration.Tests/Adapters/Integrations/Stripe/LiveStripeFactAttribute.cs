namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Stripe;

/// <summary>
/// Marks a test that talks to the real Stripe API in Test Mode, and skips it unless a test secret key is configured.
/// </summary>
/// <remarks>
/// Deciding at discovery means the run says "Skipped" and why, instead of a pass that made no call. Set
/// <c>STRIPE_TEST_SECRET_KEY</c> to a <c>sk_test_</c> key to run it; a live key is refused.
/// </remarks>
public sealed class LiveStripeFactAttribute : FactAttribute
{
    /// <summary>The environment variable that carries the Stripe Test Mode secret key.</summary>
    public const string KeyVariable = "STRIPE_TEST_SECRET_KEY";

    /// <summary>Initialises the attribute, skipping the test when no test key is configured.</summary>
    public LiveStripeFactAttribute()
    {
        var key = Environment.GetEnvironmentVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            Skip = $"No Stripe Test Mode key configured; set {KeyVariable} to a sk_test_ key to run this.";
        }
    }
}

/// <summary>
/// Marks a data-driven test that talks to the real Stripe API in Test Mode; see <see cref="LiveStripeFactAttribute"/>.
/// </summary>
public sealed class LiveStripeTheoryAttribute : TheoryAttribute
{
    /// <summary>Initialises the attribute, skipping the test when no test key is configured.</summary>
    public LiveStripeTheoryAttribute()
    {
        var key = Environment.GetEnvironmentVariable(LiveStripeFactAttribute.KeyVariable);
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            Skip = $"No Stripe Test Mode key configured; set {LiveStripeFactAttribute.KeyVariable} to a sk_test_ key to run this.";
        }
    }
}
