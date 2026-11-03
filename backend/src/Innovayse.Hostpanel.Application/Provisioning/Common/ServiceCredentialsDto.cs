using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>
/// DTO carrying hosting service credentials returned to the client after provisioning or a credentials lookup.
/// </summary>
/// <param name="Username">Control-panel login username.</param>
/// <param name="Password">Control-panel login password, or null when the panel never reveals a stored password.</param>
/// <param name="Domain">Primary domain name associated with the account.</param>
/// <param name="ServerIp">IPv4 address of the hosting server.</param>
/// <param name="CpanelUrl">Full URL to the control panel login page for this account.</param>
public sealed record ServiceCredentialsDto(string Username, string? Password, string Domain, string ServerIp, string CpanelUrl)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
