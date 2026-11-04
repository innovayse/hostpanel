using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Commands.CompleteSetup;

/// <summary>
/// Command for the first-run bootstrap: grants the Admin role to the authenticated caller.
/// </summary>
/// <remarks>
/// Carries no subject, for the same reason <c>AcceptInvitationCommand</c> does not: a field
/// naming who is being made Admin would let whoever holds the setup token make somebody else
/// Admin. The handler asks the credential.
/// </remarks>
/// <param name="SetupToken">
/// The token this installation printed to its log while setup was outstanding. Required under
/// <c>Auth:Mode=local</c>; ignored under <c>sso</c>, where no token is ever issued and the
/// accounts belong to the sign-on service.
/// </param>
public sealed record CompleteSetupCommand(string? SetupToken)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
