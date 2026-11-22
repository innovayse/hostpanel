using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request to provision a new hosting service account on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="DomainName">Primary domain name to associate with the new account.</param>
/// <param name="Username">control-panel username for the new account.</param>
/// <param name="Password">Initial password for the new account.</param>
/// <param name="Package">Hosting package name as configured on the provider server.</param>
/// <param name="ClientId">The platform client the account belongs to; a panel plugin sees it only as a per plugin reference.</param>
/// <param name="ClientEmail">The client's contact e-mail, or <see langword="null"/> when none is known; for panels that keep an owner contact.</param>
/// <param name="ClientName">The client's display name, for panels that keep an owner contact.</param>
public record ProvisionAccountOrder(
    int ServiceId,
    string DomainName,
    string Username,
    string Password,
    string Package,
    int ClientId,
    [property: SecretMember] string? ClientEmail,
    [property: SecretMember] string ClientName)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
