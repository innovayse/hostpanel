using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// The secrets a server's API is reached with. A null, empty or whitespace value means "leave the
/// stored one unchanged" on an edit; removing one is an explicit <see cref="ServerCredentialClears"/>.
/// </summary>
/// <param name="Password">API password.</param>
/// <param name="ApiToken">API token.</param>
/// <param name="AccessHash">Access hash for hosting panel authentication.</param>
public sealed record ServerCredentials(string? Password, string? ApiToken, string? AccessHash)
{
    /// <summary>Gets the value that carries no secret.</summary>
    public static ServerCredentials None { get; } = new(null, null, null);

    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
