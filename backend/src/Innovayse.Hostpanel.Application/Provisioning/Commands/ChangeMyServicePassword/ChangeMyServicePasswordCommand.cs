using System.Text;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.ChangeMyServicePassword;

/// <summary>
/// Command for a signed-in client to change the hosting account password of one of their own
/// services.
/// </summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. The admin route, which may reset any client's hosting password,
/// dispatches <c>ChangePasswordCommand</c> directly.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
/// <param name="NewPassword">The new hosting account password.</param>
public sealed record ChangeMyServicePasswordCommand(int ServiceId, string NewPassword)
    : ICallerScopedServiceMessage
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
