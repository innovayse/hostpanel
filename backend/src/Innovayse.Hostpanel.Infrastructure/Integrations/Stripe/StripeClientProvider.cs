using global::Stripe;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;

/// <summary>
/// Builds the Stripe API client once, on first use, for every Stripe adapter of a request.
/// </summary>
/// <remarks>
/// Never built when <see cref="StripeOptions.IsConfigured"/> is false: every adapter answers "not configured"
/// before it touches <see cref="Client"/>. Built eagerly in a constructor, the client threw for every caller that
/// merely depends on a Stripe adapter the moment a deployment has no Stripe secret key, including a lookup for an
/// account that never attached a card and was never going to call Stripe at all. Deferred construction means a
/// missing key only breaks the calls that actually need Stripe.
/// </remarks>
public sealed class StripeClientProvider
{
    /// <summary>The client, built on first read.</summary>
    private readonly Lazy<StripeClient> _client;

    /// <summary>Initializes the provider.</summary>
    /// <param name="options">Stripe configuration options.</param>
    public StripeClientProvider(IOptions<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = new Lazy<StripeClient>(() => new StripeClient(new StripeClientOptions
        {
            ApiKey = options.Value.SecretKey,
            ApiBase = options.Value.ApiBaseUrl.Length == 0 ? null : options.Value.ApiBaseUrl,
        }));
    }

    /// <summary>Gets the Stripe API client configured with the secret key.</summary>
    public StripeClient Client => _client.Value;
}
