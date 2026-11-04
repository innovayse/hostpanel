using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// A code that confirms a pending two-factor setup for one account.
/// </summary>
/// <param name="UserId">Whose account. Comes from the credential, never from a request body.</param>
/// <param name="Code">The six digits the authenticator app currently shows.</param>
public sealed record TwoFactorConfirmation(string UserId, [property: SecretMember] string Code)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
