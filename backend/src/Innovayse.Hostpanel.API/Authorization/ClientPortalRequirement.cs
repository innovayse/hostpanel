using Microsoft.AspNetCore.Authorization;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// The requirement behind <c>Policies.Client</c>: the caller is a client of this deployment, either because
/// they hold the stored client role or because an enabled identity plugin vouches for them.
/// </summary>
public sealed class ClientPortalRequirement : IAuthorizationRequirement
{
}
