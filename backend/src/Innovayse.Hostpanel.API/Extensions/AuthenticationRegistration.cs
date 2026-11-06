#if INNOVAYSE_SSO
using Innovayse.Auth;
#endif
using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Innovayse.Hostpanel.API.Extensions;

/// <summary>
/// Registers every authentication scheme this deployment can run under: local JWT-only mode, or
/// the sign-on provider with a local-JWT fallback for the admin panel plus the browser session cookie.
/// </summary>
public static class AuthenticationRegistration
{
    /// <summary>
    /// The scheme that stands in front of the real ones under single sign-on mode. See where it is
    /// registered, below <c>AddInnovayseAuth</c>, for what it is for.
    /// </summary>
    private const string SmartAuthScheme = "InnovayseSmart";

    /// <summary>
    /// The cookie scheme <c>Innovayse.Auth</c> registers under single sign-on mode; the one place its name is
    /// read, so every policy and the policy scheme agree on it.
    /// </summary>
    /// <remarks>
    /// A standalone build carries no <c>Innovayse.Auth</c> (see <c>InnovayseSso</c> in
    /// <c>backend/Directory.Build.props</c>), so it can only run in local mode and this value is
    /// never read there: every use sits behind a not-local-mode check that
    /// <see cref="AddHostpanelAuthentication"/> rules out at startup.
    /// </remarks>
    internal static readonly string SsoCookieScheme =
#if INNOVAYSE_SSO
        global::Innovayse.Auth.Authentication.CookieSessionHandler.SchemeName;
#else
        "InnovayseSsoUnavailable";
#endif

    /// <summary>Adds hostpanel's authentication schemes for the deployment's configured mode.</summary>
    /// <param name="services">The container.</param>
    /// <param name="configuration">Host configuration, read here for <c>Auth:Mode</c>, the JWT settings and the sign-on provider settings.</param>
    /// <returns>Whether this deployment runs in local mode, for the callers that branch on it afterwards.</returns>
    public static bool AddHostpanelAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var isLocalMode = AuthMode.IsLocal(
            configuration.GetSection(AuthModeOptions.SectionName).Get<AuthModeOptions>()?.Mode);

#if !INNOVAYSE_SSO
        // A standalone build has no Innovayse.Auth, so it can only run in local mode. Saying so here
        // beats booting into an external sign-on provider configuration whose cookie scheme was never registered.
        if (!isLocalMode)
        {
            throw new InvalidOperationException(
                "This build has no external sign-on support, so Auth:Mode (AUTH_MODE) must be 'local'. "
                + "Set AUTH_MODE=local, or rebuild with -p:InnovayseSso=true and access to the "
                + "private package feed.");
        }
#endif

        // The token signer itself is registered by AddInnovayseInfrastructure, in both modes: the
        // admin panel uses local tokens whatever the mode is. What stays here is the composition
        // root's own reading of the same three keys, for the validators built below: the signer and
        // the validators have to agree, so both fall back to the constants the implementation names.
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (isLocalMode)
        {
            // Local-only mode
            if (jwtOptions.SigningSecret.Length < 32)
            {
                throw new InvalidOperationException("Jwt:Secret must be at least 32 characters");
            }

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.RequireHttpsMetadata = false;
                    opts.TokenValidationParameters = BuildLocalJwtParameters(jwtOptions);

                    // The events merge SubjectRoles, the store every grant in this product writes
                    // to, onto the principal. Without them roles would come only from the claims
                    // baked into the token at sign-in (Identity's AspNetUserRoles), and a grant made
                    // by setup, an admin-created client or a guest checkout would authorize nothing.
                    opts.Events = new JwtBearerEvents
                    {
                        // The admin SPA does not hold this token in browser storage, where any
                        // script injected into the page could read it. The API writes it into an
                        // httpOnly cookie, as the sign-on provider path does for the same reason,
                        // and this is the half that reads it back.
                        //
                        // The Authorization header still wins where one is present, and has to: the
                        // client portal's Nuxt server calls this API machine-to-machine with a
                        // bearer token and has no browser to hold a cookie. The cookie is a
                        // fallback for the one caller that does.
                        //
                        // The cookie carries the same token: same issuer, same audience, same
                        // fifteen-minute lifetime, same validation parameters above.
                        OnMessageReceived = context =>
                        {
                            if (string.IsNullOrEmpty(context.Token)
                                && context.Request.Cookies.TryGetValue(
                                    Innovayse.Hostpanel.Shared.Infrastructure.LocalSessionCookie.Name, out var cookieToken)
                                && !string.IsNullOrEmpty(cookieToken))
                            {
                                context.Token = cookieToken;
                            }

                            return Task.CompletedTask;
                        },

                        // A token whose session version is no longer the account's own (password
                        // reset, two-factor switched off, address changed) is refused before any
                        // role is merged onto it, and so before sliding renewal can extend it.
                        OnTokenValidated = Innovayse.Hostpanel.API.Auth.LocalSessionVersionValidator.ThenEnrichRoles(),
                    };
                });
        }
        else
        {
            // single sign-on mode with local JWT fallback: admin panel uses local tokens,
            // client panel uses the sign-on provider tokens. Both are accepted.
            var localJwtIssuer = jwtOptions.Issuer;
            var ssoOptions = configuration.GetSection(SsoOptions.SectionName).Get<SsoOptions>() ?? new SsoOptions();
            if (string.IsNullOrWhiteSpace(ssoOptions.Authority) || string.IsNullOrWhiteSpace(ssoOptions.ClientId))
            {
                throw new InvalidOperationException(
                    "Auth:Mode is not 'local', so Sso:Authority (SSO_URL) and Sso:ClientId (SSO_CLIENT_ID) "
                    + "must both be set: tokens are accepted only from that issuer and only when issued to that client.");
            }

            // Back-channel logout is answered by the sign-on package, which checks every logout token's issuer
            // against Auth:BearerIssuer and refuses all of them when it is empty. Nothing else fails
            // visibly: the provider's sign-out of every application gets a 400 here and this deployment's
            // sessions simply stay alive. So an empty issuer stops the host at startup instead.
            if (string.IsNullOrWhiteSpace(configuration["Auth:BearerIssuer"]))
            {
                throw new InvalidOperationException(
                    "Auth:Mode is not 'local', so Auth:BearerIssuer (SSO_ISSUER) must be set: it is the exact issuer "
                    + "the provider stamps into its tokens, and without it every back-channel logout is refused.");
            }

            var ssoClientId = ssoOptions.ClientId;
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.Authority = ssoOptions.Authority;
                    opts.RequireHttpsMetadata = false;
                    opts.MapInboundClaims = false;
                    opts.TokenValidationParameters = new TokenValidationParameters
                    {
                        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                        NameClaimType = Innovayse.Hostpanel.Shared.Constants.JwtClaims.Sub,
                        // The provider's audience lists the resource servers a token is valid at,
                        // not the client it was issued to, so the audience cannot bind a token to
                        // this deployment. SignOnClientBinding below does, on the token's client id.
                        ValidateAudience = false,
                    };
                    // If the JWT was issued by our local server, forward to LocalJwt scheme
                    // A token this product minted itself goes to the LocalJwt scheme; anything else,
                    // including a header that is not a readable JWT at all, stays on the default.
                    opts.ForwardDefaultSelector = context =>
                    {
                        if (!BearerHeader.TryRead(context.Request, out var token))
                        {
                            return null;
                        }

                        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                        if (handler.CanReadToken(token) && handler.ReadJwtToken(token).Issuer == localJwtIssuer)
                        {
                            return LocalJwtScheme.Name;
                        }

                        return null;
                    };
                    // The subject the sign-on provider issued is the identifier this product uses, so there is
                    // nothing to map it onto and no local copy of the user to keep.
                    //
                    // The role merge itself is shared with both locally issued schemes rather than
                    // written inline here, so SubjectRoles applies to every scheme. A token issued
                    // to another client is refused before any role is read for it.
                    opts.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = SignOnClientBinding.OnTokenValidated(
                            ssoClientId,
                            SubjectRoleClaimsEnricher.OnTokenValidated()),
                    };
                })
                .AddJwtBearer(LocalJwtScheme.Name, opts =>
                {
                    opts.RequireHttpsMetadata = false;
                    opts.TokenValidationParameters = BuildLocalJwtParameters(jwtOptions);

                    // Same merge as the sign-on provider scheme above. The ForwardDefaultSelector
                    // sends every token this product minted itself here, so without it the admin
                    // panel's own credential could not see a role granted through SubjectRoles.
                    opts.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = Innovayse.Hostpanel.API.Auth.SubjectRoleClaimsEnricher.OnTokenValidated(),
                    };
                });
        }

        if (!isLocalMode)
        {
            // Cookie sessions, the third way in, and the only one a browser uses. The API runs
            // the OIDC exchange itself and the browser holds an opaque httpOnly cookie, so no
            // access or refresh token sits in browser storage where any script on the page
            // could read a 30-day credential.
            //
            // Bearer stays for the machines: the client portal's Nuxt server calls this
            // API with an external sign-on provider token, and standalone deployments keep LocalJwt. That is
            // why this is an additional scheme rather than a replacement.
#if INNOVAYSE_SSO
            services.AddInnovayseAuth(configuration);
#endif

            // Registering the cookie scheme makes it the default scheme, which is not what
            // "additional" means. Everything below turns on the difference between the
            // default POLICY and the default SCHEME:
            //
            //   [Authorize]                     uses DefaultPolicy, which names all three
            //                                   schemes below.
            //   [Authorize(Roles = "Client")]   does not. Naming a role makes MVC build a
            //                                   policy on the spot, and that policy carries
            //                                   no schemes, so it falls back to the default
            //                                   authenticate scheme: the cookie.
            //
            // With the cookie as the default, every role-annotated endpoint would ignore the
            // Authorization header and answer a valid bearer token with a bare 401, which is
            // the whole client portal, since its Nuxt server calls /api/me/* with a bearer token.
            //
            // A policy scheme keeps the intent. It authenticates nothing itself; it
            // picks the real scheme per request, so a bearer token is read as a bearer
            // token whether or not the attribute happened to name a role.
            services.AddAuthentication(opts =>
                {
                    opts.DefaultAuthenticateScheme = SmartAuthScheme;
                    opts.DefaultChallengeScheme = SmartAuthScheme;
                })
                .AddPolicyScheme(SmartAuthScheme, "Bearer when one is offered, cookie otherwise", opts =>
                {
                    opts.ForwardDefaultSelector = context =>
                        BearerHeader.TryRead(context.Request, out _)
                                // The bearer handler has its own selector that sends locally
                                // issued tokens on to LocalJwt, so this only has to choose
                                // between "a token" and "a cookie".
                                ? JwtBearerDefaults.AuthenticationScheme
                                : SsoCookieScheme;
                });

            // The cookie principal carries the sign-on provider's claims; the roles live in this
            // database. The transformation maps one onto the other.
#if INNOVAYSE_SSO
            services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation,
                SsoSessionClaimsTransformation>();
#endif
        }

        return isLocalMode;
    }

    /// <summary>
    /// The validation of a token this product signs itself, shared by local mode's only scheme and the
    /// LocalJwt scheme of the external sign-on mode, so the two can never validate differently.
    /// </summary>
    /// <param name="jwtOptions">The issuer, audience and signing secret the token signer also uses.</param>
    /// <returns>The validation parameters.</returns>
    private static TokenValidationParameters BuildLocalJwtParameters(JwtOptions jwtOptions)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningSecret)),
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = JwtClaims.Sub,
        };
    }
}
