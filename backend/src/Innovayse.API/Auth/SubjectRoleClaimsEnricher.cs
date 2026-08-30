namespace Innovayse.API.Auth;

using System.Security.Claims;
using Innovayse.Domain.Auth.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// Merges the roles held in <see cref="ISubjectRoleStore"/> onto a validated bearer token,
/// so authorization asks one store whatever minted the credential.
/// </summary>
/// <remarks>
/// <para>
/// Every grant in this product writes to <c>subject_roles</c> — <c>AuthController.SetupAsync</c>,
/// <c>AdminCreateClientHandler</c>, <c>PlaceOrderHandler</c>, <c>AcceptInvitationHandler</c> and
/// <c>MigrationPullWorker</c> all call <see cref="ISubjectRoleStore.AddAsync"/>. Only the SSO
/// scheme ever read it back, so a credential this product issued itself carried whatever roles
/// were baked into it at sign-in and nothing else. That made <c>subject_roles</c> and Identity's
/// <c>AspNetUserRoles</c> two disjoint stores under <c>AUTH_MODE=local</c>: a role granted after
/// the token was minted — which is every grant listed above — never reached a
/// <c>[Authorize(Roles = …)]</c> route, and <c>POST /api/auth/setup</c> wrote an Admin row that
/// authorized nothing while making <c>setup-required</c> answer <c>false</c> for good.
/// </para>
/// <para>
/// Attaching this to every JWT scheme in both modes is what makes the composition root's own
/// claim — that authorization is decided by <c>subject_roles</c> in both modes — true. It is
/// deliberately mode-agnostic: it knows about a role store, not about who issued the token, so
/// it sits above the <c>AUTH_MODE</c> port pair rather than inside either implementation.
/// </para>
/// <para>
/// Roles already on the token are kept rather than replaced. A token minted with a role the
/// store has not been told about is still a credential this product issued, and dropping its
/// claims here would revoke access as a side effect of reading a second source.
/// </para>
/// </remarks>
public static class SubjectRoleClaimsEnricher
{
    /// <summary>
    /// Builds the <see cref="JwtBearerEvents.OnTokenValidated"/> handler that adds the subject's
    /// stored roles to the validated principal.
    /// </summary>
    /// <returns>A handler suitable for assignment to <see cref="JwtBearerEvents.OnTokenValidated"/>.</returns>
    public static Func<TokenValidatedContext, Task> OnTokenValidated() => EnrichAsync;

    /// <summary>
    /// Adds every role the subject holds in the store as a <see cref="ClaimTypes.Role"/> claim.
    /// </summary>
    /// <param name="context">The token-validation context carrying the principal to enrich.</param>
    /// <returns>A task that completes once the roles have been merged.</returns>
    private static async Task EnrichAsync(TokenValidatedContext context)
    {
        // Both spellings, because the two schemes deliver the subject differently: SSO mode sets
        // MapInboundClaims = false so "sub" survives as itself, while the locally issued schemes
        // leave the default mapping on, which renames it to NameIdentifier. Reading only one name
        // finds nothing on the other scheme — the same trap HttpCurrentRequestContext documents.
        var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue("sub");

        if (string.IsNullOrEmpty(subject))
        {
            return;
        }

        if (context.Principal!.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<ISubjectRoleStore>();
        var held = await store.GetRolesAsync(subject, context.HttpContext.RequestAborted);

        foreach (var role in held)
        {
            // Guarded because a token may already carry the role it was minted with, and a
            // duplicate role claim makes IsInRole answer twice for one grant — harmless for
            // authorization but visible to anything that enumerates the principal's claims.
            if (!identity.HasClaim(ClaimTypes.Role, role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }
    }
}
