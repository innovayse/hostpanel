using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Host;
using Microsoft.EntityFrameworkCore;

/*
 * The SSH half of the plugin host API (#758). It is an Application service, not a bus use case: the plugin host API is an
 * Infrastructure adapter built per call, and an adapter must not dispatch handlers (the old draft invoked commands from
 * Infrastructure through IMessageBus). Both key paths are shared with the client's own use cases, SshKeyInstaller and
 * SshKeyRevocation, so a plugin's key reaches and leaves an account exactly like a key the client adds.
 *
 * The checks run in a fixed order, cheapest and least revealing first, and nothing is generated or sent before all of
 * them pass: (1) a client-area call whose plugin's manifest declares hostAccess "ssh" and whose caller holds
 * ViewModifyPasswords, the permission the portal's own SSH key routes require, so a plugin never gives a sub-user more
 * than the portal would; (2) the service is the bound client's; (3) its product is bound to the plugin; (4) SSH is
 * available on it; (5) it has room for another key; (6) the caller's SSH issue budget (the portal's ssh-keys tier, per
 * plugin and client, fail closed); and only then (7) the key pair is generated and the agent called. A foreign service and
 * a missing one answer NotFound alike. Revoke asks (1) and (2) and the key's owner, and is allowed on an inactive service:
 * taking access away never depends on the service being usable.
 *
 * The private key exists only in the returned PluginSshAccess: it is never stored, never put in a log template or an
 * activity line, and never part of an exception message.
 */

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Issues and revokes the SSH identities a plugin may hold on a hosting service of the client its call is bound to.
/// </summary>
public sealed class PluginSshAccessService
{
    /// <summary>Comment given to the generated public key; the canonical line drops it, so it never reaches a server.</summary>
    private const string IdentityComment = "hostpanel-plugin";

    /// <summary>Finds the calling plugin's manifest, for its declared host access.</summary>
    private readonly IPluginEndpointResolver _plugins;

    /// <summary>Reads the products the operator bound the plugin's slot widgets to (#823).</summary>
    private readonly IPluginSlotContextReader _slots;

    /// <summary>Resolves the service's server, provider and account.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Generates the Ed25519 pair whose private half goes to the plugin.</summary>
    private readonly ISshIdentityGenerator _identities;

    /// <summary>Records the key and installs it on the account, the path a client's key takes.</summary>
    private readonly SshKeyInstaller _installer;

    /// <summary>Takes a key off the account, the path a client's revoke takes.</summary>
    private readonly SshKeyRevocation _revocation;

    /// <summary>Stages the activity log line in the same save as the key.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Data context the key and activity line are saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The budget of SSH issues per plugin and client, taken before a key pair is generated.</summary>
    private readonly IPluginSshIssueLimiter _limiter;

    /// <summary>Initializes the service.</summary>
    /// <param name="plugins">Finds the calling plugin's manifest, for its declared host access.</param>
    /// <param name="slots">Reads the products the operator bound the plugin's slot widgets to (#823).</param>
    /// <param name="resolver">Resolves the service's server, provider and account.</param>
    /// <param name="identities">Generates the Ed25519 pair whose private half goes to the plugin.</param>
    /// <param name="installer">Records the key and installs it on the account.</param>
    /// <param name="revocation">Takes a key off the account.</param>
    /// <param name="activity">Stages the activity log line in the same save as the key.</param>
    /// <param name="context">Data context the key and activity line are saved through.</param>
    /// <param name="limiter">The budget of SSH issues per plugin and client, taken before a key pair is generated.</param>
    public PluginSshAccessService(
        IPluginEndpointResolver plugins,
        IPluginSlotContextReader slots,
        ISshServiceContextResolver resolver,
        ISshIdentityGenerator identities,
        SshKeyInstaller installer,
        SshKeyRevocation revocation,
        IActivityLogger activity,
        IHostpanelDbContext context,
        IPluginSshIssueLimiter limiter)
    {
        _limiter = limiter;
        _plugins = plugins;
        _slots = slots;
        _resolver = resolver;
        _identities = identities;
        _installer = installer;
        _revocation = revocation;
        _activity = activity;
        _context = context;
    }

    /// <summary>Issues an SSH identity on <paramref name="serviceId"/> to the plugin of <paramref name="binding"/>.</summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the plugin asks about.</param>
    /// <param name="label">What the key is for, shown to the client; trimmed, 1 to <see cref="SshLimits.MaxLabelLength"/> characters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The access, or <c>NotAllowed</c>, <c>NotFound</c>, <c>Unavailable</c>, <c>LimitReached</c>, <c>ServerUnavailable</c>.</returns>
    /// <exception cref="InvalidOperationException">The identity generator wrote a public key that does not parse (a host fault).</exception>
    public async Task<PluginSshIssueOutcome> IssueAsync(
        PluginCallBinding binding, int serviceId, string label, CancellationToken ct)
    {
        var trimmed = label.Trim();
        if (!DeclaresSsh(binding) || trimmed.Length is 0 or > SshLimits.MaxLabelLength)
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var context = await ResolveOwnedAsync(binding, serviceId, ct);
        if (context is null)
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.NotFound);
        }

        if (!await IsBoundAsync(binding, context.Service.ProductId, ct))
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.NotAllowed);
        }

        if (context.State is not SshAccessState.Available || context.Agent is not { } agent || context.Account is not { } account)
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.Unavailable);
        }

        // Asked before a key pair is generated, so a full service never makes a private key that is thrown away. The
        // installer asks again under the save, which is the check that holds against a concurrent add.
        var held = await _context.SshKeys
            .CountAsync(k => k.ClientServiceId == context.Service.Id && k.SyncState != SshKeySyncState.Revoked, ct);
        if (held >= SshLimits.MaxKeysPerService)
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.LimitReached);
        }

        // Fail closed and before the expensive steps: a spent budget generates no key pair and calls no agent. Reported
        // as ServerUnavailable, the SDK's "try again later".
        if (!await _limiter.TryAcquireAsync(binding.PluginId, context.Service.ClientId, ct))
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.ServerUnavailable);
        }

        // The gate above passed at the start of the call; the operator may have switched the plugin off or removed it
        // since. Asked again right before a private key exists, so a plugin that is gone never gets one.
        if (!await IsStillAvailableAsync(binding, ct))
        {
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var identity = _identities.Generate(IdentityComment);
        if (!SshPublicKey.TryParse(identity.PublicKey, out var publicKey, out _) || publicKey is null)
        {
            // Never the key text in the message: only that the generator broke its own contract.
            throw new InvalidOperationException("The SSH identity generator produced an unparsable public key.");
        }

        var installed = await _installer.InstallAsync(context, trimmed, publicKey, binding.PluginId, ct);
        if (installed.IsFailure || installed.Value is not { } key)
        {
            return PluginSshIssueOutcome.Refused(FailureFor(installed.ErrorCode));
        }

        // A removal that committed while the key was being installed may already have run its revoke pass, which then
        // could not see this key. Asked once more after the install: a plugin that is gone has the key taken back here,
        // so no key outlives its plugin, and the private key is never handed out.
        if (!await IsStillAvailableAsync(binding, ct))
        {
            await _revocation.RevokeAsync(context, key, ct);
            await _context.SaveChangesAsync(ct);
            return PluginSshIssueOutcome.Refused(PluginHostAccessFailure.NotAllowed);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(
                context.Service.ClientId,
                $"Issued SSH key \"{key.Label}\" ({key.Fingerprint}) to plugin {binding.PluginId} for service #{context.Service.Id}"),
            ct);
        await _context.SaveChangesAsync(ct);

        // The pinned fingerprint is the agent's; it describes the customer-facing address only when both are one endpoint.
        var samePoint = string.Equals(agent.SshHostname, agent.AgentHost, StringComparison.OrdinalIgnoreCase)
            && agent.SshPort == agent.AgentPort;
        return PluginSshIssueOutcome.Issued(new PluginSshAccess(
            key.KeyId,
            agent.SshHostname,
            agent.SshPort,
            account.Username,
            context.Service.Domain,
            identity.PrivateKey,
            samePoint ? agent.AgentHostKeyFingerprint : null));
    }

    /// <summary>
    /// Revokes a key the host issued to the plugin of <paramref name="binding"/>. The product need no longer be bound:
    /// taking access back never depends on still being allowed to grant it.
    /// </summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the key is on.</param>
    /// <param name="keyId">The key, as the issued access named it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> once the key is off the account, otherwise <c>NotAllowed</c>, <c>NotFound</c> (also for the
    /// client's own keys and other plugins' keys) or <c>ServerUnavailable</c> (the key records why). An inactive service
    /// is not refused: removing access never depends on the service being usable.
    /// </returns>
    public async Task<PluginHostAccessFailure?> RevokeAsync(
        PluginCallBinding binding, int serviceId, Guid keyId, CancellationToken ct)
    {
        if (!DeclaresSsh(binding))
        {
            return PluginHostAccessFailure.NotAllowed;
        }

        var context = await ResolveOwnedAsync(binding, serviceId, ct);
        if (context is null)
        {
            return PluginHostAccessFailure.NotFound;
        }

        var key = await _context.SshKeys.FirstOrDefaultAsync(
            k => k.ClientServiceId == context.Service.Id && k.KeyId == keyId, ct);
        if (key is null || !key.IsActive || !key.IsIssuedTo(binding.PluginId))
        {
            return PluginHostAccessFailure.NotFound;
        }

        if (!await _revocation.RevokeAsync(context, key, ct))
        {
            // The key records why; it stays visible to the client as possibly still able to log in.
            await _context.SaveChangesAsync(ct);
            return PluginHostAccessFailure.ServerUnavailable;
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(
                context.Service.ClientId,
                $"Plugin {binding.PluginId} revoked SSH key \"{key.Label}\" ({key.Fingerprint}) from service #{context.Service.Id}"),
            ct);
        await _context.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>
    /// Whether the call may ask for SSH at all: a client-area call with a client whose caller holds
    /// <see cref="ClientPermission.ViewModifyPasswords"/> (what the portal's own SSH key routes require), from a loaded plugin
    /// whose manifest declares <see cref="PluginHostAccess.Ssh"/>. Answered from the binding and the manifest, never from
    /// the plugin.
    /// </summary>
    /// <param name="binding">The call.</param>
    /// <returns><see langword="true"/> when the call may go on to the service checks.</returns>
    private bool DeclaresSsh(PluginCallBinding binding)
    {
        if (binding.Area is not PluginArea.Client
            || binding.ClientId is null
            || !binding.Permissions.HasFlag(ClientPermission.ViewModifyPasswords))
        {
            return false;
        }

        var manifest = _plugins.Find(binding.PluginId);
        return manifest is not null && manifest.HostAccess.Contains(PluginHostAccess.Ssh, StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves the service's SSH context when it belongs to the binding's client. The binding is the ownership rule here:
    /// it is the client the call was verified for, the same one the plugin's caller reference and store describe.
    /// </summary>
    /// <param name="binding">The call; its client is set (checked by <see cref="DeclaresSsh"/>).</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The context, or <see langword="null"/> for a missing service and another client's alike.</returns>
    private async Task<SshServiceContext?> ResolveOwnedAsync(PluginCallBinding binding, int serviceId, CancellationToken ct)
    {
        var context = await _resolver.ResolveAsync(serviceId, ct);
        return context is not null && context.Service.ClientId == binding.ClientId ? context : null;
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
    private async Task<bool> IsBoundAsync(PluginCallBinding binding, int productId, CancellationToken ct)
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
    private async Task<bool> IsStillAvailableAsync(PluginCallBinding binding, CancellationToken ct)
    {
        return await _plugins.PrepareAsync(binding.PluginId, ct) is not null;
    }

    /// <summary>The SDK's reason for an installer refusal.</summary>
    /// <param name="code">The installer's failure code.</param>
    /// <returns>The reason the plugin is told.</returns>
    private static PluginHostAccessFailure FailureFor(string? code)
    {
        return code switch
        {
            HostpanelErrorCodes.SSH_KEY_LIMIT_REACHED => PluginHostAccessFailure.LimitReached,
            HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE
                or HostpanelErrorCodes.SSH_KEY_ALREADY_INSTALLED
                or HostpanelErrorCodes.SSH_KEY_INVALID => PluginHostAccessFailure.Unavailable,
            _ => PluginHostAccessFailure.ServerUnavailable,
        };
    }
}
