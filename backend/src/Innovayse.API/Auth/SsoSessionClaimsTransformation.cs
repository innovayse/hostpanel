using System.Security.Claims;
using Innovayse.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace Innovayse.API.Auth;

/// <summary>
/// Maps a cookie-session principal onto this application's own users and roles.
/// </summary>
/// <remarks>
/// The Bearer path does this inside <c>OnTokenValidated</c>: it provisions the SSO
/// user locally, swaps <see cref="ClaimTypes.NameIdentifier"/> to the local user id
/// (so <c>GetUserId()</c> returns something the database knows), and loads roles.
/// The cookie handler is a shared library and knows nothing about this product's user
/// table, so the same mapping happens here instead — <see cref="IClaimsTransformation"/>
/// runs after any scheme succeeds, and this one acts only on the cookie scheme so the
/// Bearer path is not provisioned twice.
///
/// Without this, an admin signing in through the cookie flow authenticates and is then
/// refused by every <c>AdminOnly</c> policy: the principal would carry the SSO's claims
/// and none of the roles this database grants.
/// </remarks>
public sealed class SsoSessionClaimsTransformation(IUserService userService) : IClaimsTransformation
{
    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity
            || identity.AuthenticationType != global::Innovayse.Auth.CookieSessionHandler.SchemeName)
            return principal;

        // Already mapped on a previous middleware pass of the same request.
        if (identity.HasClaim(c => c.Type == "hostpanel:mapped")) return principal;

        var sub = principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value;
        if (sub is null || email is null) return principal;

        var firstName = principal.FindFirst("given_name")?.Value ?? string.Empty;
        var lastName = principal.FindFirst("family_name")?.Value ?? string.Empty;
        await userService.ProvisionSsoUserAsync(sub, email, firstName, lastName, CancellationToken.None);

        var localUser = await userService.FindBySsoSubjectAsync(sub, CancellationToken.None);
        if (localUser is not null)
        {
            // Replace the alias the library added (sub) with the local id GetUserId()
            // expects — keeping both would make FindFirst return whichever came first.
            foreach (var claim in identity.FindAll(ClaimTypes.NameIdentifier).ToList())
                identity.RemoveClaim(claim);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, localUser.Value.Id));

            var roles = await userService.GetRolesAsync(localUser.Value.Id, CancellationToken.None);
            foreach (var role in roles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        identity.AddClaim(new Claim("hostpanel:mapped", "1"));
        return principal;
    }
}
