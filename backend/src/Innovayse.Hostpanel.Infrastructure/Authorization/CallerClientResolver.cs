using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PluginGrant = Innovayse.SDK.Plugins.Identity.ClientAccess;

namespace Innovayse.Hostpanel.Infrastructure.Authorization;

/// <summary>
/// Resolves the caller's account and permissions: through the enabled identity plugin when there is one,
/// otherwise from the client records and the links stored beside them. It keeps the answer for the rest of
/// the request (in <see cref="ICallerClientMemo"/>) until <see cref="Invalidate"/>.
/// </summary>
public sealed class CallerClientResolver : ICallerClientResolver
{
    /// <summary>The module's data context, for the owner record and the stored links.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Who is asking. Nothing tells this type whose account to resolve.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>The enabled identity plugin, which decides for every principal when it exists.</summary>
    private readonly IIdentityPluginResolver _plugins;

    /// <summary>Logs why the identity plugin could not decide; the plugin's own message and secrets are never logged.</summary>
    private readonly ILogger<CallerClientResolver> _logger;

    /// <summary>Keeps the answer for the whole request, across the scope of the policy and the scope of the bus.</summary>
    private readonly ICallerClientMemo _memo;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the owner record and the stored links.</param>
    /// <param name="caller">Who is asking. Nothing tells this type whose account to resolve.</param>
    /// <param name="plugins">The enabled identity plugin, which decides for every principal when it exists.</param>
    /// <param name="memo">Keeps the answer for the whole request, across the scope of the policy and the scope of the bus.</param>
    /// <param name="logger">Logs why the identity plugin could not decide.</param>
    public CallerClientResolver(
        IHostpanelDbContext context,
        ICurrentUserService caller,
        IIdentityPluginResolver plugins,
        ICallerClientMemo memo,
        ILogger<CallerClientResolver> logger)
    {
        _context = context;
        _caller = caller;
        _plugins = plugins;
        _memo = memo;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CallerClientAccess> ResolveAsync(CancellationToken ct)
    {
        // Nobody identified, nothing to keep: the answer is None and no memo is touched.
        if (_caller.UserId is not { Length: > 0 } userId)
        {
            return CallerClientAccess.None;
        }

        // A request is one caller, and the policy, the permission filter and the use case all ask. The memo
        // hands the answer back only to the subject it was resolved for.
        if (_memo.TryGet(userId, out var kept))
        {
            return kept;
        }

        // A cancelled resolution throws before the answer is kept, so nothing half-read is remembered.
        var access = await ResolveFreshAsync(ct);
        _memo.Set(userId, access);

        return access;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        _memo.Forget();
    }

    /// <summary>Reads the caller's account from the plugin or the stored records, without looking at the kept answer.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's access, see <see cref="ResolveAsync"/>.</returns>
    private async Task<CallerClientAccess> ResolveFreshAsync(CancellationToken ct)
    {
        var userId = _caller.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return CallerClientAccess.None;
        }

        // An enabled identity plugin replaces the default rule for everyone: it is the system that knows
        // who belongs to which account, so falling back to the owner or link rules would let a person
        // the plugin refused in through a record the plugin never vouched for.
        IClientResolver? plugin;
        try
        {
            plugin = await _plugins.ResolveAsync(ct);
        }
        catch (IdentityPluginConflictException conflict)
        {
            // Fail closed: choosing one of the plugins would move people onto another account.
            _logger.LogWarning(
                "Identity plugins {PluginIds} are all enabled; callers are refused until at most one stays enabled.",
                string.Join(", ", conflict.PluginIds));
            return CallerClientAccess.Unavailable;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Activating the plugin failed (its settings, its assembly): same refusal, type only in the log.
            _logger.LogWarning(
                "Activating the identity plugin failed with {ExceptionType}; callers are refused until it activates.",
                ex.GetType().Name);
            return CallerClientAccess.Unavailable;
        }

        if (plugin is not null)
        {
            return await ResolveThroughPluginAsync(plugin, ct);
        }

        // The account owner is the user the client record itself names; it is never also a stored link.
        var ownedId = await _context.Clients.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (ownedId is not null)
        {
            return new CallerClientAccess(ownedId, true, ClientPermission.All);
        }

        var links = await _context.ClientUsers.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.ClientId, u.Permissions })
            .ToListAsync(ct);
        if (links.Count == 0)
        {
            return CallerClientAccess.None;
        }

        // Start from everything and keep only what every link grants. Masking with All first drops a
        // bit this product never defined, so an unknown flag in the table can only ever grant nothing.
        var common = ClientPermission.All;
        foreach (var link in links)
        {
            common &= link.Permissions & ClientPermission.All;
        }

        // With several links nothing says which account is meant, so the caller acts for none.
        int? clientId = links.Count == 1 ? links[0].ClientId : null;

        return new CallerClientAccess(clientId, false, common);
    }

    /// <summary>Asks the identity plugin which account the principal acts for and what it may do there.</summary>
    /// <param name="plugin">The enabled identity plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The linked client with the mapped permissions, never as owner. A principal the plugin refuses acts for
    /// no account and is marked <see cref="CallerClientAccess.Refused"/>, so the order route does not take them for a
    /// first time buyer. An account no client is linked to yet keeps its mapped permissions with no client id, so the
    /// order flow that creates the client is still authorised while every read answers "no client profile". A plugin
    /// that throws or times out gives <see cref="CallerClientAccess.Unavailable"/>, logged by exception type only.
    /// </returns>
    private async Task<CallerClientAccess> ResolveThroughPluginAsync(IClientResolver plugin, CancellationToken ct)
    {
        if (_caller.Principal is not { } principal)
        {
            return CallerClientAccess.Refused;
        }

        PluginGrant? granted;
        try
        {
            granted = await plugin.ResolveAsync(principal, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // A timeout, a transport fault or a plugin bug: nobody knows who this person is, so nobody is
            // let in. The message and the stack stay out of the log, a plugin may put its secrets in them.
            _logger.LogWarning(
                "Identity plugin {Plugin} failed with {ExceptionType} ({Reason}); the caller is refused until it answers.",
                plugin.GetType().Name,
                ex.GetType().Name,
                ex is TimeoutException or OperationCanceledException ? "timeout" : "error");
            return CallerClientAccess.Unavailable;
        }

        if (granted is null)
        {
            return CallerClientAccess.Refused;
        }

        var account = granted.Account;
        var clientId = await _context.Clients.AsNoTracking()
            .Where(c => c.ExternalAccountProvider == account.Provider && c.ExternalAccountId == account.Id)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        // Owner-only actions stay closed: the plugin knows members of an account, not its holder.
        return new CallerClientAccess(clientId, false, IdentityPluginPermissions.From(granted), account);
    }
}
