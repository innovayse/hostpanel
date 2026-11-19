using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>A database the host holds on a hosting service for a plugin.</summary>
/// <param name="Id">The host's id of the database.</param>
/// <param name="Name">The database name.</param>
/// <param name="Username">The database user's name.</param>
/// <param name="Host">The host name to connect to.</param>
/// <param name="Port">The port to connect to.</param>
/// <param name="Password">The user's password when the database was just created, otherwise <see langword="null"/>; never stored.</param>
public sealed record HostedDatabase(Guid Id, string Name, string Username, string Host, int Port, string? Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
