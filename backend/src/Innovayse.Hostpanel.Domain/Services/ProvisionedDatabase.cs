using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// A database as the hosting provider answered for it. The password exists only when the provider just created the
/// database; it is handed on once and never stored, and the printed form masks it.
/// </summary>
/// <param name="Name">The database name on the server.</param>
/// <param name="Username">The database user's name on the server.</param>
/// <param name="Password">The user's password when the provider created the database now, otherwise <see langword="null"/>.</param>
public sealed record ProvisionedDatabase(string Name, string Username, string? Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
