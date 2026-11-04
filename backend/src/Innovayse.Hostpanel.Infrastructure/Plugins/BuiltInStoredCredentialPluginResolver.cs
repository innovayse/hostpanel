using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Resolves the stored credential capability of every payment gateway module: the built-in Stripe gateway first,
/// then payment plugins through <see cref="StoredCredentialPluginResolver"/>. The host sees one capability whichever
/// it is, so a plugin gateway and the built-in one are charged the same way.
/// </summary>
public sealed class BuiltInStoredCredentialPluginResolver : IStoredCredentialPluginResolver
{
    /// <summary>Resolves the capability of payment plugins.</summary>
    private readonly StoredCredentialPluginResolver _plugins;

    /// <summary>The built-in Stripe gateway's capability.</summary>
    private readonly StripeStoredCredentialPlugin _stripe;

    /// <summary>Initializes a new instance of the <see cref="BuiltInStoredCredentialPluginResolver"/> class.</summary>
    /// <param name="plugins">Resolves the capability of payment plugins.</param>
    /// <param name="stripe">The built-in Stripe gateway's capability.</param>
    public BuiltInStoredCredentialPluginResolver(StoredCredentialPluginResolver plugins, StripeStoredCredentialPlugin stripe)
    {
        _plugins = plugins;
        _stripe = stripe;
    }

    /// <inheritdoc/>
    public Task<IStoredCredentialPlugin?> ResolveAsync(string module, CancellationToken ct)
    {
        if (module != BuiltInPaymentModules.Stripe)
        {
            return _plugins.ResolveAsync(module, ct);
        }

        // Without the gateway's keys no card can have been saved and no call may be made.
        return Task.FromResult<IStoredCredentialPlugin?>(_stripe.IsConfigured ? _stripe : null);
    }
}
