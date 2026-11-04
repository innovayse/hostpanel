using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// Where and under which account a <see cref="ClientService"/> is hosted: the server, the domain, the
/// panel reference, the dedicated address and the panel credentials. Staff correct them together on the
/// service's hosting account section, and <see cref="ClientService.RecordHostingAccount"/> applies them
/// as one change.
/// </summary>
/// <param name="ServerId">The server the account lives on, or <see langword="null"/> when none is assigned.</param>
/// <param name="Domain">The account's primary domain, or <see langword="null"/>.</param>
/// <param name="ProvisioningRef">The account's reference on the panel, or <see langword="null"/>.</param>
/// <param name="DedicatedIp">The dedicated IP address, or <see langword="null"/>.</param>
/// <param name="Username">The account's username on the panel, or <see langword="null"/>.</param>
/// <param name="Password">The account's password on the panel, or <see langword="null"/>.</param>
public sealed record ServiceHostingAccount(
    int? ServerId,
    string? Domain,
    string? ProvisioningRef,
    string? DedicatedIp,
    string? Username,
    string? Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
