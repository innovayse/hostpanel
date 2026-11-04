using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

/*
 * One binding builds both services and nothing is derived here: the store's scope is the binding's ClientRef, the very
 * value the call runner hands the plugin on its caller, and the facts follow the binding's client and permissions. A second
 * derivation of the reference would be a second source that could drift. Public with public parameter types because the
 * call runner is built in Wolverine's generated code; the services it hands out are internal, built per call and never
 * resolved from the container.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// <see cref="IPluginCallServices"/> of the host: every call gets a <see cref="PluginHostApi"/> bound to the call's
/// binding, a <see cref="PluginStore"/> scoped to the plugin and the binding's client reference (plugin wide when the call
/// has none) and a fresh <see cref="PluginStoreQuotaSignal"/> the store raises and the runner reads. Registered scoped:
/// the services live as long as the request that runs the call.
/// </summary>
public sealed class HostPluginCallServices : IPluginCallServices
{
    /// <summary>The request's data context, written by the store.</summary>
    private readonly AppDbContext _context;

    /// <summary>Reads the client, service and invoice rows the host API shares.</summary>
    private readonly IPluginHostFactsReader _facts;

    /// <summary>Reads the email address and language of the client's owner.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Resolves the currency the client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The plugin API limits; the store's quota per scope is read from it.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Initializes the factory.</summary>
    /// <param name="context">The request's data context, written by the store.</param>
    /// <param name="facts">Reads the client, service and invoice rows the host API shares.</param>
    /// <param name="identity">Reads the email address and language of the client's owner.</param>
    /// <param name="payerCurrency">Resolves the currency the client is billed in.</param>
    /// <param name="options">The plugin API limits; the store's quota per scope is read from it.</param>
    public HostPluginCallServices(
        AppDbContext context,
        IPluginHostFactsReader facts,
        IIdentityProvider identity,
        IPayerCurrencyResolver payerCurrency,
        IOptions<PluginApiOptions> options)
    {
        _context = context;
        _facts = facts;
        _identity = identity;
        _payerCurrency = payerCurrency;
        _options = options;
    }

    /// <inheritdoc/>
    public PluginCallServices Create(PluginCallBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        // One signal per call: a refusal in one call never colours another call's answer.
        var quota = new PluginStoreQuotaSignal();
        return new PluginCallServices(
            new PluginHostApi(binding, _facts, _identity, _payerCurrency),
            new PluginStore(binding.PluginId, binding.ClientRef, _context, _options.Value.StoreQuota, quota),
            quota);
    }
}
