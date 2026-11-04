using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Clients.Commands.ConfirmMyEmailChange;

/// <summary>
/// Makes the caller's address change effective with the token mailed to the new address.
/// </summary>
/// <remarks>
/// Whose account is not on the command: the handler asks the credential, so a link forwarded to
/// someone else changes nothing for them, and a token only ever matches the account it was issued to.
/// </remarks>
/// <param name="Email">The new address, as carried by the link.</param>
/// <param name="Token">The token from the link.</param>
public sealed record ConfirmMyEmailChangeCommand(string Email, string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
