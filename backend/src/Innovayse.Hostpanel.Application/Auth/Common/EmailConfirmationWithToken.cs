using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// An email address confirmed with the token the account holder received by email.
/// </summary>
/// <param name="Email">The account's email address.</param>
/// <param name="Token">The confirmation token from the email link.</param>
public sealed record EmailConfirmationWithToken(string Email, string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
