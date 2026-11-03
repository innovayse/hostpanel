using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// An account holder's request to move their sign-in to a new address, before the new address has
/// been proven.
/// </summary>
/// <param name="Subject">Whose account. Comes from the credential, never from a request body.</param>
/// <param name="NewEmail">The address to move to.</param>
/// <param name="CurrentPassword">The account's current password, proving the person and not just the session.</param>
public sealed record EmailChangeStart(string Subject, string NewEmail, string CurrentPassword)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
