using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Offers the stored credential capability of a payment plugin. It asks <see cref="IPaymentPluginResolver"/> for the
/// module and checks whether the plugin it got also implements <see cref="IStoredCredentialPlugin"/>, so a disabled,
/// missing or misconfigured plugin has no capability and the gates stay in one place.
/// </summary>
public sealed class StoredCredentialPluginResolver : IStoredCredentialPluginResolver
{
    /// <summary>Resolves the payment plugin whose capability is wanted.</summary>
    private readonly IPaymentPluginResolver _payments;

    /// <summary>Initializes a new instance of the <see cref="StoredCredentialPluginResolver"/> class.</summary>
    /// <param name="payments">Resolves the payment plugin whose capability is wanted.</param>
    public StoredCredentialPluginResolver(IPaymentPluginResolver payments)
    {
        _payments = payments;
    }

    /// <inheritdoc/>
    public async Task<IStoredCredentialPlugin?> ResolveAsync(string module, CancellationToken ct)
    {
        var plugin = await _payments.ResolveAsync(module, ct);
        return plugin as IStoredCredentialPlugin;
    }
}
