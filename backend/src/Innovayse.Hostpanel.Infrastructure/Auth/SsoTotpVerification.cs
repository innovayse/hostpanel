using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>
/// The result of submitting a code. <see cref="BackupCodes"/> is present when this
/// completed an enrolment and null when it satisfied a re-auth challenge instead.
/// </summary>
/// <param name="BackupCodes">Recovery codes when the code completed an enrolment; null otherwise.</param>
public sealed record SsoTotpVerification([property: SecretMember] IReadOnlyList<string>? BackupCodes)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
