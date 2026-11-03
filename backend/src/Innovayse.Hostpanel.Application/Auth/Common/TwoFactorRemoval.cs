using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// The proof an account holder gives to switch two-factor authentication off.
/// </summary>
/// <param name="UserId">Whose account. Comes from the credential, never from a request body.</param>
/// <param name="Code">A current code from the authenticator app.</param>
/// <param name="CurrentPassword">
/// The account's current password. Required where this deployment owns the accounts; where an
/// external sign-on provider owns them, the provider asks for its own proof and this is not read.
/// </param>
public sealed record TwoFactorRemoval(string UserId, [property: SecretMember] string Code, string? CurrentPassword)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
