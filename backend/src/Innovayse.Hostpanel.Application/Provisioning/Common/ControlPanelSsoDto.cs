using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>A time-limited control-panel single-sign-on address.</summary>
/// <param name="Url">The address that signs the holder into the hosting control panel.</param>
public sealed record ControlPanelSsoDto([property: SecretMember] string Url)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
