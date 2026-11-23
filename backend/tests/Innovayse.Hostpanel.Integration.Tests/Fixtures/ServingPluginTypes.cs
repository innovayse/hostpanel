using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The plugin types the <c>endpoints</c> capability is proven for, so every every-type test runs one case per type and a type
/// added here is covered by all of them (#1357). An app plugin is not listed: it is served by its type.
/// </summary>
public static class ServingPluginTypes
{
    /// <summary>Gets the plugin types that serve only when their manifest declares the capability.</summary>
    public static IReadOnlyList<PluginType> Types { get; } =
    [
        PluginType.Payment,
        PluginType.Registrar,
        PluginType.Provisioning,
        PluginType.Fulfilment,
    ];

    /// <summary>Gets <see cref="Types"/> as theory rows, one per type.</summary>
    public static TheoryData<PluginType> All
    {
        get
        {
            var rows = new TheoryData<PluginType>();
            foreach (var type in Types)
            {
                rows.Add(type);
            }

            return rows;
        }
    }
}
