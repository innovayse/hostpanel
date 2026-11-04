using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Services.Commands.SetupService;

/// <summary>Command to set up a pending service with hosting details and trigger provisioning.</summary>
/// <param name="ServiceId">The service to set up.</param>
/// <param name="Domain">The domain name for the hosting account.</param>
/// <param name="Username">The desired hosting account username.</param>
/// <param name="Password">The desired hosting account password.</param>
public sealed record SetupServiceCommand(int ServiceId, string Domain, string Username, string Password)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
