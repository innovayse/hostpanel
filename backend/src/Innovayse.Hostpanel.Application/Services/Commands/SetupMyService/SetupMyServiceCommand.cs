using System.Text;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Services.Commands.SetupMyService;

/// <summary>
/// Command for a signed-in client to supply the hosting details of one of their own pending
/// services and have it provisioned.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. <c>SetupServiceCommand</c>, which this delegates to, names no
/// caller and is what any future staff-side route would dispatch.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
/// <param name="Domain">The domain name for the hosting account.</param>
/// <param name="Username">The desired hosting account username.</param>
/// <param name="Password">The desired hosting account password.</param>
public sealed record SetupMyServiceCommand(
    int ServiceId,
    string Domain,
    string Username,
    string Password) : ICallerScopedServiceMessage
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
