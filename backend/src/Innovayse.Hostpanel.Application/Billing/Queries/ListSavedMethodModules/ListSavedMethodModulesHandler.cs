using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListSavedMethodModules;

/// <summary>
/// Handles <see cref="ListSavedMethodModulesQuery"/>: the built-in card gateway and every payment plugin whose
/// stored credential capability resolves, which means installed, enabled and configured.
/// </summary>
public sealed class ListSavedMethodModulesHandler
{
    /// <summary>Lists the installed plugins.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Initializes the handler.</summary>
    /// <param name="plugins">Lists the installed plugins.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    public ListSavedMethodModulesHandler(IPluginRegistry plugins, StoredCredentialGateway gateway)
    {
        _plugins = plugins;
        _gateway = gateway;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it takes no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The modules a method can be saved with; empty when none offers it.</returns>
    public async Task<Result<IReadOnlyList<SavedMethodModuleDto>>> HandleAsync(ListSavedMethodModulesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var modules = new List<SavedMethodModuleDto>();
        if (await _gateway.IsOfferedAsync(BuiltInPaymentModules.Stripe, ct))
        {
            modules.Add(new SavedMethodModuleDto(BuiltInPaymentModules.Stripe, "Credit/Debit Card"));
        }

        foreach (var manifest in _plugins.GetLoadedManifests().Where(m => m.Type == PluginType.Payment))
        {
            if (await _gateway.IsOfferedAsync(manifest.Id, ct))
            {
                modules.Add(new SavedMethodModuleDto(manifest.Id, $"Bank Card ({manifest.Name})"));
            }
        }

        return Result.Success<IReadOnlyList<SavedMethodModuleDto>>(modules);
    }
}
