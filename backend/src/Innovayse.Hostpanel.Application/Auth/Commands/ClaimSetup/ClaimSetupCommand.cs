using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Commands.ClaimSetup;

/// <summary>
/// Command for the first-run claim from the setup endpoint. Carries no subject: who is being made
/// Admin is the caller, resolved from the credential by the shared write.
/// </summary>
/// <param name="SetupToken">The token this installation printed to its log; ignored under <c>sso</c>.</param>
public sealed record ClaimSetupCommand(string? SetupToken)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
