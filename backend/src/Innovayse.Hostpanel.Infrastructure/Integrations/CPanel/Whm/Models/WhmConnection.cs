using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;

/// <summary>Where and how the WHM client reaches one WHM server.</summary>
/// <param name="BaseUrl">
/// Base URL of WHM, scheme and port included and no trailing slash, e.g.
/// <c>https://whm.example.com:2087</c>.
/// </param>
/// <param name="Username">The WHM user the API token belongs to, usually <c>root</c> or a reseller.</param>
/// <param name="ApiToken">The WHM API token sent in the <c>Authorization</c> header; never logged.</param>
public sealed record WhmConnection(string BaseUrl, string Username, string ApiToken)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
