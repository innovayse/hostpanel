using Innovayse.Hostpanel.Application.Auth.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Mints the credentials a signed-in person carries: the short-lived access token an API call
/// presents, and the opaque random string a refresh flow would exchange for a new one.
/// </summary>
/// <remarks>
/// Minting a token is infrastructure work, it needs a signing secret the deployment configures
/// and a library that knows the wire format, so the contract is declared here, from the caller's
/// side, and implemented in Infrastructure. Nothing in this signature names JWT, a claim, or a
/// signing algorithm: the Application layer asks for a credential for one person, and what that
/// credential looks like on the wire is the implementation's business alone.
/// </remarks>
public interface IJwtService
{
    /// <summary>Issues a short-lived access token for one person.</summary>
    /// <param name="claims">Who the credential is for and what it says about them.</param>
    /// <returns>The signed credential, ready to be handed to the caller as a bearer token.</returns>
    public string GenerateAccessToken(AccessTokenClaims claims);

    /// <summary>How long one issued access token stays valid.</summary>
    public TimeSpan AccessTokenLifetime { get; }

    /// <summary>Re-issues an access token that is close to expiry (sliding renewal).</summary>
    /// <param name="accessToken">The token the caller presented.</param>
    /// <returns>
    /// A fresh token carrying the same claims and the original sign-in time, or <see langword="null"/>
    /// when the token is invalid, expired, not yet in the last third of its lifetime, predates
    /// renewal support, carries no session version, or the session has reached its absolute
    /// maximum age. The session version is carried unchanged, so a renewed token is refused as
    /// soon as the account's version moves on. The renewed expiry is
    /// capped at that maximum age.
    /// </returns>
    public string? RenewAccessToken(string accessToken);

    /// <summary>Issues a fresh refresh token.</summary>
    /// <returns>
    /// A cryptographically random, opaque string. It carries no claims and nothing can be read
    /// out of it: it is only ever compared against a stored copy.
    /// </returns>
    public string GenerateRefreshToken();
}
