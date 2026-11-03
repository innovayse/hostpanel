using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Commands.EnableTwoFactor;

/// <summary>Switches two-factor authentication on, once a code has proved the enrolment.</summary>
/// <remarks>
/// Whose account is not on the command at all. The handler asks the credential, so there is
/// no field a caller could set to act on somebody else's account.
/// </remarks>
/// <param name="Code">The six digits the authenticator app currently shows.</param>
public sealed record EnableTwoFactorCommand([property: SecretMember] string Code)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
