using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>What creating a subscription for a customer needs.</summary>
/// <param name="OwnerLogin">The customer the subscription belongs to.</param>
/// <param name="Domain">The subscription's primary domain.</param>
/// <param name="IpAddress">The server address the domain is hosted on; Plesk requires one.</param>
/// <param name="PlanName">The service plan the subscription is created on.</param>
/// <param name="SystemUser">The system (FTP/SSH) user created with the web space.</param>
/// <param name="Password">The system user's password; never logged.</param>
public sealed record PleskNewSubscription(
    string OwnerLogin,
    string Domain,
    string IpAddress,
    string PlanName,
    string SystemUser,
    string Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
