using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>Where and how the Plesk client reaches one Plesk server.</summary>
/// <param name="BaseUrl">
/// Base URL of the server, scheme and port included and no trailing slash, e.g.
/// <c>https://plesk.example.com:8443</c>. Plesk answers the XML API and the panel on the same port.
/// </param>
/// <param name="ApiKey">The secret API key sent in the <c>KEY</c> header; never logged.</param>
public sealed record PleskConnection(string BaseUrl, string ApiKey)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
