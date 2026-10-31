using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// What the signed-in user may do on the client account they act for, and that account's id, for the
/// client portal. Everything is answered from <see cref="ICallerClientResolver"/>, the single source
/// of truth, so nothing here resolves the caller a second way.
/// </summary>
/// <remarks>
/// <para>
/// The client portal's permission filter asks this port for the permissions, and every client-facing
/// use case asks it for the account, so an owner and a linked user are resolved the same way
/// everywhere and a linked user sees exactly what the permission attribute lets through. The
/// ownership ports (<c>ITicketOwnership</c>, <c>IDomainOwnership</c>, <c>IServiceOwnership</c>,
/// <c>IInvoiceOwnership</c>) compare a resource against the account resolved here. It resolves the
/// caller itself through <c>ICurrentUserService</c>: the caller is never named by the request, so a
/// body or a route cannot talk its way into somebody else's account.
/// </para>
/// <para>
/// It fails closed. A caller who owns no client record and is linked to none, a caller linked to
/// several accounts, and a caller the request does not identify act for no account.
/// </para>
/// </remarks>
public interface IClientAccess
{
    /// <summary>Reads the permissions the current credential holds on the account it acts for.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="ClientPermission.All"/> for the owner of a client record; otherwise what the
    /// caller's stored link grants, limited to the flags this product defines (a stored bit nobody
    /// defined grants nothing). When the caller is linked to several accounts the result is only what
    /// every link grants. <see cref="ClientPermission.None"/> when the caller owns no record, is
    /// linked to none, or is not identified at all.
    /// </returns>
    Task<ClientPermission> GetCallerPermissionsAsync(CancellationToken ct);

    /// <summary>Tells whether the identity plugin that decides the caller's account could not answer for this request.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the plugin threw, timed out, or two are enabled: the permission filter then answers
    /// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> instead of a permission refusal the caller could take for permanent.
    /// </returns>
    Task<bool> IsIdentityUnavailableAsync(CancellationToken ct);

    /// <summary>Resolves the id of the account the current credential acts for, owner or linked user.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The account's primary key, or a failed <see cref="Result{T}"/> carrying
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account, or <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the
    /// identity plugin could not answer.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task<Result<int>> RequireClientIdAsync(CancellationToken ct);

    /// <summary>Resolves the id of the account the current credential owns, refusing a linked user.</summary>
    /// <remarks>
    /// For what belongs to the account holder alone: the profile and the sign-in details it shows.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The owned account's primary key, or a failed <see cref="Result{T}"/> carrying
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller owns no client record, or <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the
    /// identity plugin could not answer.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task<Result<int>> RequireOwnClientIdAsync(CancellationToken ct);
}
