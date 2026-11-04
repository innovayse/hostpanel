using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Commands.SignIn;

/// <summary>
/// The credentials a local-mode sign-in presents. Anonymous by nature: the caller is who the
/// credentials say, so the command names the account by email.
/// </summary>
/// <remarks>
/// The bus validates the body before the credential check runs: a body that omits a
/// field reaches the validator as <see langword="null"/> and is answered as a 400, instead of
/// reaching the identity store as a null argument.
/// </remarks>
/// <param name="Email">The account's email address; <see langword="null"/> when the body omitted it.</param>
/// <param name="Password">The password to verify; <see langword="null"/> when the body omitted it.</param>
public sealed record SignInCommand(string? Email, string? Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
