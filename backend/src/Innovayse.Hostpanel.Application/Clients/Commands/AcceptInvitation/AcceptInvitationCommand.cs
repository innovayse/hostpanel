using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Clients.Commands.AcceptInvitation;

/// <summary>Command to accept an invitation and link the provider-authenticated user to the client account.</summary>
/// <remarks>
/// Carries no user id. Who is accepting is resolved inside the handler from the credential:
/// a field naming the subject would let whoever holds an invitation token link a different
/// account to the client, permissions and all.
/// </remarks>
/// <param name="Token">The invitation token.</param>
public sealed record AcceptInvitationCommand(string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
