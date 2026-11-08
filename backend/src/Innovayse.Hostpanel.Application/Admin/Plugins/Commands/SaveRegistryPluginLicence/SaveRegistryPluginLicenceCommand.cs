using System.Text;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SaveRegistryPluginLicence;

/// <summary>
/// Stores the licence key of a paid or private registry plugin, encrypted, replacing any key stored before. The key is
/// sent to the registry with the details, the install and the update check of that plugin, and is never returned.
/// </summary>
/// <param name="PluginId">The plugin id; set from the route.</param>
/// <param name="Key">The licence key the operator bought; write only, never printed.</param>
public sealed record SaveRegistryPluginLicenceCommand(string PluginId, [property: SecretMember] string Key) : IDemoRestricted
{
    /// <summary>Prints the record without its secret member; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
