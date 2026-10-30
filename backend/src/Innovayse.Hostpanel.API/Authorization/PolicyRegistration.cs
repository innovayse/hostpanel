using Innovayse.Hostpanel.API.Extensions;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Shared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Registers the authorization policies every controller authorizes against, and, under single sign-on,
/// the default policy every scheme-less <c>[Authorize]</c> falls back to.
/// </summary>
public static class PolicyRegistration
{
    /// <summary>Adds hostpanel's authorization policies.</summary>
    /// <param name="services">The container.</param>
    /// <param name="isLocalMode">
    /// Whether this deployment runs in local mode, as <see cref="Innovayse.Hostpanel.API.Extensions.AuthenticationRegistration.AddHostpanelAuthentication"/>
    /// resolved it: local mode has one scheme to authenticate against, so it needs neither a
    /// non-default default policy nor an explicit scheme list on every role policy.
    /// </param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddHostpanelAuthorizationPolicies(this IServiceCollection services, bool isLocalMode)
    {
        services.AddAuthorization(opts =>
        {
            if (!isLocalMode)
            {
                // Accept tokens from either single sign-on or local JWT scheme
                opts.DefaultPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme, Innovayse.Hostpanel.Shared.Constants.LocalJwtScheme.Name,
                    AuthenticationRegistration.SsoCookieScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            }

            // Every policy authenticates against the same schemes the default policy accepts; under
            // single sign-on that is the sign-on provider's bearer, the admin panel's local JWT and the browser session cookie.
            void AddRolePolicy(string name, params string[] roles)
            {
                opts.AddPolicy(name, p =>
                {
                    if (!isLocalMode)
                    {
                        p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, Innovayse.Hostpanel.Shared.Constants.LocalJwtScheme.Name,
                            AuthenticationRegistration.SsoCookieScheme);
                    }

                    if (roles.Length == 0)
                    {
                        p.RequireAuthenticatedUser();
                    }
                    else
                    {
                        p.RequireRole(roles);
                    }
                });
            }

            AddRolePolicy(Policies.Member);
            AddRolePolicy(Policies.Admin, Roles.Admin);

            // The client portal is open to the stored client role and, when an identity plugin is enabled, to the
            // members it vouches for (they never receive the role; see ClientPortalAccessHandler).
            opts.AddPolicy(Policies.Client, p =>
            {
                if (!isLocalMode)
                {
                    p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, Innovayse.Hostpanel.Shared.Constants.LocalJwtScheme.Name,
                        AuthenticationRegistration.SsoCookieScheme);
                }

                p.RequireAuthenticatedUser();
                p.AddRequirements(new ClientPortalRequirement());
            });
        });

        // Scoped: the handler asks the per-request caller resolver, which must not outlive the request.
        services.AddScoped<IAuthorizationHandler, ClientPortalAccessHandler>();

        return services;
    }
}
