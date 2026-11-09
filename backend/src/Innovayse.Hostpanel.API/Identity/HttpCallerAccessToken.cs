using System.IdentityModel.Tokens.Jwt;
using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.API.Identity;

/// <summary>
/// Reads the caller's bearer token off the current request's <c>Authorization</c> header, but only a
/// token the external sign-on provider issued: a token this product minted itself is never forwarded.
/// </summary>
/// <remarks>
/// A request-scoped adapter: it lives in the API because it reads <see cref="HttpContext"/>, and
/// it parses the header through <see cref="BearerHeader"/>, the one place that header is parsed.
/// In external sign-on mode the admin panel signs in with a token this product issues; handing that
/// to the provider would send a local credential to an outside service, so its issuer decides.
/// </remarks>
public sealed class HttpCallerAccessToken : ICallerAccessToken
{
    /// <summary>Source of the current request.</summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>The issuer this product signs its own tokens with; a token from it is never forwarded.</summary>
    private readonly string _localIssuer;

    /// <summary>Initializes the reader.</summary>
    /// <param name="httpContextAccessor">Source of the current request.</param>
    /// <param name="jwtOptions">The local token settings, for the issuer this product signs with.</param>
    public HttpCallerAccessToken(IHttpContextAccessor httpContextAccessor, IOptions<JwtOptions> jwtOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _localIssuer = jwtOptions.Value.Issuer;
    }

    /// <inheritdoc/>
    public string? GetBearerToken()
    {
        if (_httpContextAccessor.HttpContext is not { } context
            || !BearerHeader.TryRead(context.Request, out var token))
        {
            return null;
        }

        // The signature was already checked by authentication; here only the issuer is read, to tell
        // the provider's token from one this product minted. Anything unreadable is not forwarded.
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        var issuer = handler.ReadJwtToken(token).Issuer;
        return string.IsNullOrEmpty(issuer) || string.Equals(issuer, _localIssuer, StringComparison.Ordinal)
            ? null
            : token;
    }
}
