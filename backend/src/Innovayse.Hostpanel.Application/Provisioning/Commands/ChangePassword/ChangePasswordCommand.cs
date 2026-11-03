using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePassword;

/// <summary>Changes the hosting account password of a provisioned service on its server.</summary>
/// <param name="ServiceId">Client service primary key. Overlaid from the route.</param>
/// <param name="NewPassword">The new password to set.</param>
public sealed record ChangePasswordCommand(int ServiceId, string NewPassword)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
