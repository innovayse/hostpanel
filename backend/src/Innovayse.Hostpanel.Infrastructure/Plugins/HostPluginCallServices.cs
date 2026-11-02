using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Persistence;

/*
 * One binding builds both services, so the host facts and the store cannot be bound to two different clients: the
 * store's client scope is derived here from the same client id the facts are read for. Public with public parameter
 * types because the call runner is built in Wolverine's generated code; the two services it hands out are internal,
 * built per call and never resolved from the container.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// <see cref="IPluginCallServices"/> of the host: every call gets a <see cref="PluginHostApi"/> bound to the call's
/// client and a <see cref="PluginStore"/> scoped to the plugin and that client's opaque reference (plugin wide when the
/// call has no client). Both are built from the one <see cref="PluginCallBinding"/>, so the facts and the store can
/// never be about two different clients. Registered scoped: the services live as long as the request that runs the call.
/// </summary>
public sealed class HostPluginCallServices : IPluginCallServices
{
    /// <summary>The request's data context, read by the host API and written by the store.</summary>
    private readonly AppDbContext _context;

    /// <summary>Reads the email address and language of the client's owner.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Resolves the currency the client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Derives the plugin's opaque reference of the client, the store's client scope.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Initializes the factory.</summary>
    /// <param name="context">The request's data context, read by the host API and written by the store.</param>
    /// <param name="identity">Reads the email address and language of the client's owner.</param>
    /// <param name="payerCurrency">Resolves the currency the client is billed in.</param>
    /// <param name="references">Derives the plugin's opaque reference of the client, the store's client scope.</param>
    public HostPluginCallServices(
        AppDbContext context,
        IIdentityProvider identity,
        IPayerCurrencyResolver payerCurrency,
        IPluginReferences references)
    {
        _context = context;
        _identity = identity;
        _payerCurrency = payerCurrency;
        _references = references;
    }

    /// <inheritdoc/>
    public PluginCallServices Create(PluginCallBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var clientRef = binding.ClientId is { } clientId ? _references.ForClient(binding.PluginId, clientId) : null;
        return new PluginCallServices(
            new PluginHostApi(binding.ClientId, _context, _identity, _payerCurrency),
            new PluginStore(binding.PluginId, clientRef, _context));
    }
}
