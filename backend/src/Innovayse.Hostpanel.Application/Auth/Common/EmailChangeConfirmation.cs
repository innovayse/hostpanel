using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// The token mailed to a new address, coming back to make the address change effective.
/// </summary>
/// <param name="Subject">Whose account. Comes from the credential, never from a request body.</param>
/// <param name="NewEmail">The address the token was issued for.</param>
/// <param name="Token">The token from the link sent to <paramref name="NewEmail"/>.</param>
public sealed record EmailChangeConfirmation(string Subject, string NewEmail, string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
