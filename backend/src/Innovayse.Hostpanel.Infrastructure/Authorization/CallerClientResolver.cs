using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Authorization;

/// <summary>
/// Resolves the caller's account and permissions: through the enabled identity plugin when there is one,
/// otherwise from the client records and the links stored beside them.
/// </summary>
public sealed class CallerClientResolver : ICallerClientResolver
{
    /// <summary>The module's data context, for the owner record and the stored links.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Who is asking. Nothing tells this type whose account to resolve.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>The enabled identity plugin, which decides for every principal when it exists.</summary>
    private readonly IIdentityPluginResolver _plugins;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the owner record and the stored links.</param>
    /// <param name="caller">Who is asking. Nothing tells this type whose account to resolve.</param>
    /// <param name="plugins">The enabled identity plugin, which decides for every principal when it exists.</param>
    public CallerClientResolver(IHostpanelDbContext context, ICurrentUserService caller, IIdentityPluginResolver plugins)
    {
        _context = context;
        _caller = caller;
        _plugins = plugins;
    }

    /// <inheritdoc/>
    public async Task<CallerClientAccess> ResolveAsync(CancellationToken ct)
    {
        var userId = _caller.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return CallerClientAccess.None;
        }

        // An enabled identity plugin replaces the default rule for everyone: it is the system that knows
        // who belongs to which account, so falling back to the owner or link rules would let a person
        // the plugin refused in through a record the plugin never vouched for.
        if (await _plugins.ResolveAsync(ct) is { } plugin)
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
    /// no account. An account no client is linked to yet keeps its mapped permissions with no client id, so the
    /// order flow that creates the client is still authorised while every read answers "no client profile".
    /// </returns>
    private async Task<CallerClientAccess> ResolveThroughPluginAsync(IClientResolver plugin, CancellationToken ct)
    {
        if (_caller.Principal is not { } principal || await plugin.ResolveAsync(principal, ct) is not { } granted)
        {
            return CallerClientAccess.None;
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
