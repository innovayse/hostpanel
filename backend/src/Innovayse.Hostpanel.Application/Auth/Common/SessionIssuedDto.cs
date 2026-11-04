using System.Text;
using Innovayse.Hostpanel.Shared.Models.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// A finished local sign-in: the access token and how long it lives. The token also goes into the
/// httpOnly session cookie, which the API layer sets from <see cref="ISessionGrant.SessionToken"/>.
/// </summary>
/// <param name="AccessToken">The signed access token.</param>
/// <param name="ExpiresIn">The token's lifetime, in seconds.</param>
public sealed record SessionIssuedDto(string AccessToken, int ExpiresIn) : ISessionGrant
{
    /// <summary>The token the session cookie carries: the access token itself.</summary>
    string? ISessionGrant.SessionToken => AccessToken;

    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
