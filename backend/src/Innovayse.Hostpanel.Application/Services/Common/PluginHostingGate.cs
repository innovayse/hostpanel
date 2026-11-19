using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;

/*
 * The one gate every plugin host access to a client's hosting passes (#758, #1294). The checks are the cheap, least
 * revealing ones, asked by the caller in a fixed order: (1) Declares, from the binding and the manifest only; (2)
 * ResolveOwnedAsync, the service is the bound client's; (3) IsBoundAsync, the operator bound the plugin to the product;
 * and, right before anything irreversible, IsStillAvailableAsync. Only this type reads the plugin manifest and the slot
 * bindings for host access, so a new kind of access cannot skip a check: it is another value of the host access kind
 * and the same gate, never a copy of it. The kind is the SDK's PluginHostAccess name the manifest declares.
 */

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Decides whether a plugin call may reach a client's hosting, for one kind of host access. Answers from the call's
/// binding, the plugin's manifest and the operator's product bindings, never from anything the plugin says.
/// </summary>
public sealed class PluginHostingGate
{
    /// <summary>Finds the calling plugin's manifest, for its declared host access.</summary>
    private readonly IPluginEndpointResolver _plugins;

    /// <summary>Reads the products the operator bound the plugin's slot widgets to (#823).</summary>
    private readonly IPluginSlotContextReader _slots;

    /// <summary>Initializes the gate.</summary>
    /// <param name="plugins">Finds the calling plugin's manifest, for its declared host access.</param>
    /// <param name="slots">Reads the products the operator bound the plugin's slot widgets to (#823).</param>
    public PluginHostingGate(IPluginEndpointResolver plugins, IPluginSlotContextReader slots)
    {
        _plugins = plugins;
        _slots = slots;
    }

    /// <summary>
    /// Whether the call may ask for <paramref name="hostAccess"/> at all: a client-area call with a client whose caller
    /// holds <see cref="ClientPermission.ViewModifyPasswords"/> (what the portal's own SSH key routes require), from a
    /// loaded plugin whose manifest declares the kind. Answered from the binding and the manifest, never from the plugin.
    /// </summary>
    /// <param name="binding">The call.</param>
    /// <param name="hostAccess">The host access kind, one of the SDK's <see cref="PluginHostAccess"/> names.</param>
    /// <returns><see langword="true"/> when the call may go on to the service checks.</returns>
    public bool Declares(PluginCallBinding binding, string hostAccess)
    {
        if (binding.Area is not PluginArea.Client
            || binding.ClientId is null
            || !binding.Permissions.HasFlag(ClientPermission.ViewModifyPasswords))
        {
            return false;
        }

        var manifest = _plugins.Find(binding.PluginId);
        return manifest is not null && manifest.HostAccess.Contains(hostAccess, StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves the service's context when it belongs to the binding's client. The binding is the ownership rule here:
    /// it is the client the call was verified for, the same one the plugin's caller reference and store describe.
    /// </summary>
    /// <typeparam name="TContext">The context the kind of access works with.</typeparam>
    /// <param name="binding">The call; its client is set (checked by <see cref="Declares"/>).</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="resolve">Resolves the context of a service, or <see langword="null"/> when there is none.</param>
    /// <param name="clientOf">The client a resolved context belongs to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The context, or <see langword="null"/> for a missing service and another client's alike.</returns>
    public async Task<TContext?> ResolveOwnedAsync<TContext>(
        PluginCallBinding binding,
        int serviceId,
        Func<int, CancellationToken, Task<TContext?>> resolve,
        Func<TContext, int> clientOf,
        CancellationToken ct)
        where TContext : class
    {
        var context = await resolve(serviceId, ct);
        return context is not null && clientOf(context) == binding.ClientId ? context : null;
    }

    /// <summary>
    /// Whether the operator bound one of the plugin's slot widgets to <paramref name="productId"/> (#823), the one place
    /// the host records an operator's decision about which products a plugin serves. Only operator rows count, and only
    /// for a product that still exists: a manifest's own <c>productIds</c> are the plugin author's claim, and a plugin that
    /// listed every product id would otherwise reach every hosting account before anyone bound it. Nothing bound means no
    /// hosting at all.
    /// </summary>
    /// <param name="binding">The call.</param>
    /// <param name="productId">The product of the service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> only when an operator binding row of the plugin names the product.</returns>
    public async Task<bool> IsBoundAsync(PluginCallBinding binding, int productId, CancellationToken ct)
    {
        var rows = await _slots.ListBindingsAsync(binding.PluginId, null, ct);
        return rows.Any(row => row.ProductExists && row.ProductId == productId);
    }

    /// <summary>
    /// Whether the plugin is still switched on, configured and served, read again from the settings rather than from the
    /// call's start. Builds nothing.
    /// </summary>
    /// <param name="binding">The call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> while the plugin's routes would still answer.</returns>
    public async Task<bool> IsStillAvailableAsync(PluginCallBinding binding, CancellationToken ct)
    {
        return await _plugins.PrepareAsync(binding.PluginId, ct) is not null;
    }
}
