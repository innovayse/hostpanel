using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Commands.DisableTwoFactor;

/// <summary>Switches two-factor authentication off.</summary>
/// <remarks>
/// Whose account is not on the command at all. The handler asks the credential, so there is
/// no field a caller could set to disarm somebody else's second factor.
/// </remarks>
/// <param name="Code">A current code from the authenticator app.</param>
/// <param name="CurrentPassword">
/// The account's current password, required where this deployment owns the accounts. Never logged.
/// </param>
public sealed record DisableTwoFactorCommand([property: SecretMember] string Code, string? CurrentPassword = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
