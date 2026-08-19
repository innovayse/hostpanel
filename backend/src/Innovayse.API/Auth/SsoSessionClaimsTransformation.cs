using System.Security.Claims;
using Innovayse.Domain.Auth.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace Innovayse.API.Auth;

/// <summary>
/// Adds this application's roles to a cookie-session principal.
/// </summary>
/// <remarks>
/// The cookie handler is a shared library and knows nothing about this product's roles,
/// so they are attached here — <see cref="IClaimsTransformation"/> runs after any scheme
/// succeeds, and this one acts only on the cookie scheme.
///
/// Without it, an admin signing in through the cookie flow authenticates and is then
/// refused by every <c>AdminOnly</c> policy: the principal would carry the SSO's claims
/// and none of the roles this database grants.
///
/// <para>
/// This used to do two more things, and does neither now. It provisioned a local copy of
/// the SSO user on first sign-in — a copy that was never updated again, so a changed name
/// or address in the SSO never reached this product. And it replaced
/// <see cref="ClaimTypes.NameIdentifier"/> with that copy's local id. The subject the SSO
/// issued is now the identifier this product uses throughout, so there is nothing left to
/// swap and no second record to drift.
/// </para>
/// </remarks>
public sealed class SsoSessionClaimsTransformation(ISubjectRoleStore roles) : IClaimsTransformation
{
    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity
            || identity.AuthenticationType != global::Innovayse.Auth.CookieSessionHandler.SchemeName)
            return principal;

        // Already mapped on a previous middleware pass of the same request.
        if (identity.HasClaim(c => c.Type == "hostpanel:mapped")) return principal;

        var subject = principal.FindFirst("sub")?.Value;
        if (subject is null) return principal;

        foreach (var role in await roles.GetRolesAsync(subject, CancellationToken.None))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        identity.AddClaim(new Claim("hostpanel:mapped", "1"));
        return principal;
    }
}
