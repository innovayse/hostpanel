using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// What it takes to create a locally stored account.
/// </summary>
/// <param name="Email">The account's email address, also used as its user name.</param>
/// <param name="Password">The plain-text password to hash and store.</param>
/// <param name="FirstName">Given name, or <see langword="null"/> when the person did not give one.</param>
/// <param name="LastName">Family name, or <see langword="null"/> when the person did not give one.</param>
public sealed record CreateUserInput(string Email, string Password, string? FirstName, string? LastName)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
