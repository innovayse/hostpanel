using Innovayse.Hostpanel.Application.Clients.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The single source of truth for which client account the signed-in user acts for and with which
/// client permissions.
/// </summary>
/// <remarks>
/// It resolves the caller itself through <c>ICurrentUserService</c>, so a body, a route or a header
/// cannot name whose account applies. The resolution order is the enabled identity plugin, which when
/// present decides alone (its purchase and payment method rights map onto the client permission flags),
/// then the client record the user owns, then the user's client link. <c>IClientAccess</c> (the
/// permission filter and the client-facing use cases) and the ownership ports all delegate here, so an
/// owner, a linked user and a plugin member are resolved the same way everywhere. A new source of
/// client users joins the order in the one implementation of this port and every caller follows.
/// </remarks>
public interface ICallerClientResolver
{
    /// <summary>Resolves the account the current credential acts for and the permissions it holds there.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// When an identity plugin is enabled: the client linked to the account it names, with the permissions
    /// mapped from its rights and never as owner, or <see cref="CallerClientAccess.None"/> when it refuses
    /// the principal. Otherwise the owner of a client record acts for it with all permissions. A caller linked to exactly one
    /// account acts for it with the permissions the link grants, limited to the flags this product
    /// defines. A caller linked to several accounts holds only what every link grants and acts for
    /// none of them. A caller who owns no record, is linked to none, or is not identified gets
    /// <see cref="CallerClientAccess.None"/>.
    /// </returns>
    Task<CallerClientAccess> ResolveAsync(CancellationToken ct);
}
