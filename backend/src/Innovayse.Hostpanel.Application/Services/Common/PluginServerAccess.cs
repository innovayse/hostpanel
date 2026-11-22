using System.Text;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// The server a plugin call reaches a client's panel on: the connection every provisioning consumer already shares,
/// plus the two members only a plugin call needs. The password is read on this call path alone, so no other consumer of
/// <see cref="ServerConnectionDto"/> ever holds it. Server-side only; it prints through <see cref="SecretMembers"/>.
/// </summary>
/// <param name="Connection">The server's connection details, the one mapping of a server to its connection.</param>
/// <param name="UseSsl"><see langword="true"/> when the panel's API is reached over HTTPS.</param>
/// <param name="Password">
/// The panel user's password, or <see langword="null"/> when none is stored; never printed.
/// </param>
public sealed record PluginServerAccess(ServerConnectionDto Connection, bool UseSsl, string? Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
