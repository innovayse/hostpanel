using System.Text;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Admin.Users.Commands.SetUserPassword;

/// <summary>
/// Command to set a new password for a user directly, without the holder's involvement
/// (admin action).
/// </summary>
/// <param name="Id">
/// The person's subject. Nullable because the controller overlays it from the route
/// (<c>command with { Id = id }</c>): a non-nullable member of a bound body is implicitly
/// required, so the request would be refused before the overlay could run.
/// </param>
/// <param name="Password">The new password to set.</param>
public sealed record SetUserPasswordCommand(string? Id, string Password) : IDemoRestricted
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
