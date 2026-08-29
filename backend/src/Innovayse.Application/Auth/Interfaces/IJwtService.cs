namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Mints the credentials a signed-in person carries: the short-lived access token an API call
/// presents, and the opaque random string a refresh flow would exchange for a new one.
/// </summary>
/// <remarks>
/// Minting a token is infrastructure work -- it needs a signing secret the deployment configures
/// and a library that knows the wire format -- so the contract is declared here, from the caller's
/// side, and implemented in Infrastructure. Nothing in this signature names JWT, a claim, or a
/// signing algorithm: the Application layer asks for a credential for one person, and what that
/// credential looks like on the wire is the implementation's business alone.
/// </remarks>
public interface IJwtService
{
    /// <summary>Issues a short-lived access token for one person.</summary>
    /// <param name="userId">Identifier the credential is issued for; it becomes the token's subject.</param>
    /// <param name="email">The person's email address.</param>
    /// <param name="firstName">
    /// Given name, or <see langword="null"/> when unknown -- left off the credential entirely
    /// rather than carried as an empty value, so a consumer can tell "not known" from "blank".
    /// </param>
    /// <param name="lastName">Family name, or <see langword="null"/> when unknown -- omitted the same way.</param>
    /// <param name="roles">
    /// Every role the person holds, as the authorization layer will read them. An empty list is
    /// legitimate and means the person holds none, not that roles were not looked up.
    /// </param>
    /// <param name="emailVerified">
    /// Whether the address has been confirmed. Carried on the credential so a consumer deciding
    /// what an unconfirmed account may do does not have to ask the database again.
    /// </param>
    /// <returns>The signed credential, ready to be handed to the caller as a bearer token.</returns>
    string GenerateAccessToken(
        string userId,
        string email,
        string? firstName,
        string? lastName,
        IReadOnlyList<string> roles,
        bool emailVerified = true);

    /// <summary>Issues a fresh refresh token.</summary>
    /// <returns>
    /// A cryptographically random, opaque string. It carries no claims and nothing can be read
    /// out of it -- it is only ever compared against a stored copy.
    /// </returns>
    string GenerateRefreshToken();
}
