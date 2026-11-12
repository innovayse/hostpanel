using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// The permission check a use case makes for itself. The portal's actions are guarded by the permission filter, but a
/// use case that is reached by a route open to guests (an order's payment start) never meets it, so a use case that must
/// hold a signed-in caller to a permission asks here, with the same rule the filter applies.
/// </summary>
public static class ClientAccessExtensions
{
    /// <summary>Refuses unless the caller holds every flag of <paramref name="permission"/> on the account they act for.</summary>
    /// <remarks>
    /// Whether the identity plugin could answer is the caller's to settle first (<see cref="IClientAccess.RequireClientIdAsync"/>);
    /// an unanswered plugin holds no permission, so this refuses it as a permission it does not hold.
    /// </remarks>
    /// <param name="access">The caller's account and permissions.</param>
    /// <param name="permission">The permission the use case needs; several flags mean all of them.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CLIENT_PERMISSION_DENIED</c> when the caller lacks the permission.</returns>
    public static async Task<Result> EnsureCallerHoldsAsync(
        this IClientAccess access, ClientPermission permission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);

        var held = await access.GetCallerPermissionsAsync(ct);

        return (held & permission) == permission
            ? Result.Success()
            : Result.FailureCode(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }
}
