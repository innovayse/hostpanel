using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;

/*
 * The service connection half of the plugin host API (#1357): the server and the account the panel holds for one of the
 * caller's services, so a plugin route on a service tab can talk to that panel. An Application service for the same
 * reason PluginSshAccessService is, and it asks the shared PluginHostingGate in the same order, cheapest and least
 * revealing first: (1) a client-area call whose plugin's manifest declares hostAccess "serviceConnection" and whose
 * caller holds ViewModifyPasswords, the permission the portal needs to show a client a password; (2) the service is the bound
 * client's; (3) the operator bound the plugin to the product; (4) the service is active and has an account on a server;
 * (5) the plugin is still switched on; and only then (6) the audit line is saved and the stored credentials are handed
 * over. A foreign service and a missing one answer NotFound alike, and a refusal writes no audit line.
 *
 * The credentials exist only in the returned value: they are never put in a log template, an activity line or an
 * exception message.
 */

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Hands a plugin the server connection of a hosting service of the client its call is bound to.
/// </summary>
public sealed class PluginServiceConnectionService
{
    /// <summary>The gate every host access passes: access, ownership, operator binding and availability.</summary>
    private readonly PluginHostingGate _gate;

    /// <summary>Reads the service and its server with the stored credentials.</summary>
    private readonly IPluginServiceConnectionReader _reader;

    /// <summary>Stages the activity log line in the same save as the read.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Data context the activity line is saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the service.</summary>
    /// <param name="gate">The gate every host access passes: access, ownership, operator binding and availability.</param>
    /// <param name="reader">Reads the service and its server with the stored credentials.</param>
    /// <param name="activity">Stages the activity log line in the same save as the read.</param>
    /// <param name="context">Data context the activity line is saved through.</param>
    public PluginServiceConnectionService(
        PluginHostingGate gate,
        IPluginServiceConnectionReader reader,
        IActivityLogger activity,
        IHostpanelDbContext context)
    {
        _gate = gate;
        _reader = reader;
        _activity = activity;
        _context = context;
    }

    /// <summary>
    /// Resolves the connection of <paramref name="serviceId"/> for the plugin of <paramref name="binding"/>.
    /// </summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the plugin asks about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection, or <c>NotAllowed</c>, <c>NotFound</c> or <c>Unavailable</c>.</returns>
    public async Task<PluginHostOutcome<GrantedPluginServiceConnection>> ResolveAsync(
        PluginCallBinding binding, int serviceId, CancellationToken ct)
    {
        if (!_gate.Declares(binding, PluginHostAccess.ServiceConnection))
        {
            return PluginHostOutcome<GrantedPluginServiceConnection>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var connection = await _gate.ResolveOwnedAsync(binding, serviceId, _reader.ResolveAsync, c => c.ClientId, ct);
        if (connection is null)
        {
            return PluginHostOutcome<GrantedPluginServiceConnection>.Refused(PluginHostAccessFailure.NotFound);
        }

        if (!await _gate.IsBoundAsync(binding, connection.ProductId, ct))
        {
            return PluginHostOutcome<GrantedPluginServiceConnection>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        if (connection.Status is not ServiceStatus.Active
            || connection.Server is null
            || string.IsNullOrWhiteSpace(connection.ProvisioningRef))
        {
            return PluginHostOutcome<GrantedPluginServiceConnection>.Refused(PluginHostAccessFailure.Unavailable);
        }

        // The gate above passed at the start of the call; the operator may have switched the plugin off or removed it
        // since. Asked again right before the credentials leave, so a plugin that is gone never receives them.
        if (!await _gate.IsStillAvailableAsync(binding, ct))
        {
            return PluginHostOutcome<GrantedPluginServiceConnection>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        // Written before the credentials are handed over: a read that cannot be recorded is not given. The plugin and
        // the service only, never a host name, user name or credential.
        await _activity.RecordAsync(
            new ActivityLogEntry(
                connection.ClientId,
                $"Plugin {binding.PluginId} read the server connection of service #{connection.ServiceId}"),
            ct);
        await _context.SaveChangesAsync(ct);

        return PluginHostOutcome<GrantedPluginServiceConnection>.Granted(
            new GrantedPluginServiceConnection(connection.ServiceId, connection.ProvisioningRef, connection.Server));
    }
}
