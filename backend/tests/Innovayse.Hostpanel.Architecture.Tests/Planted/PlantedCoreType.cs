using Innovayse.Plugins.Planted;

namespace Innovayse.Hostpanel.Architecture.Tests.Planted;

/// <summary>A stand-in for a core type that breaks the rule by holding a plugin type in a field.</summary>
public sealed class PlantedCoreType
{
    /// <summary>The forbidden reference the rule must catch.</summary>
    private readonly PlantedPluginContract _contract = new();

    /// <summary>Exposes the field so the compiler keeps it.</summary>
    public object Contract => _contract;
}
