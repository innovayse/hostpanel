using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// A new password set with a reset token the account holder received by email.
/// </summary>
/// <param name="Email">The account's email address.</param>
/// <param name="Token">The reset token from the email link.</param>
/// <param name="NewPassword">The password to set.</param>
public sealed record PasswordResetWithToken(string Email, string Token, string NewPassword)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
