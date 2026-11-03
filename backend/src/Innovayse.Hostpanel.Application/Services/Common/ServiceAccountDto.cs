using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>The hosting account a service was provisioned as.</summary>
/// <param name="DedicatedIp">Dedicated IP address.</param>
/// <param name="Username">Hosting account username.</param>
/// <param name="Password">Hosting account password.</param>
/// <param name="ProvisioningRef">External provisioning reference (e.g., hosting account name).</param>
public sealed record ServiceAccountDto(string? DedicatedIp, string? Username, string? Password, string? ProvisioningRef)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
