using System.Text;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// What a provisioning provider needs to reach one server's control panel. Server-side only; it
/// carries the panel's access hash and API token and is never sent to a browser.
/// </summary>
/// <param name="Id">The server.</param>
/// <param name="Name">Its display name, for log lines.</param>
/// <param name="Module">Its control panel module, which picks the provider.</param>
/// <param name="Hostname">The host the panel answers on.</param>
/// <param name="IpAddress">Its primary address, or <see langword="null"/> when only the hostname is known.</param>
/// <param name="AccessHash">The panel's API access hash, or <see langword="null"/> when none is configured.</param>
/// <param name="Username">The panel user the API token belongs to, e.g. the WHM user; <see langword="null"/> when unset.</param>
/// <param name="ApiToken">The panel's API token, e.g. a WHM API token, or <see langword="null"/> when none is configured; never printed.</param>
public sealed record ServerConnectionDto(
    int Id,
    string Name,
    ServerModule Module,
    string Hostname,
    string? IpAddress,
    string? AccessHash,
    string? Username = null,
    string? ApiToken = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
