using Innovayse.Hostpanel.Architecture.Tests.Planted;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The core is plugin neutral: Domain, Application, Infrastructure and API reference no namespace reserved
/// for deployment-specific plugins (<see cref="ReservedPluginNamespaces"/>), apart from the optional identity
/// provider package. A deployment plugs in through an SDK contract, never through a reference from the core.
/// </summary>
public sealed class PluginNeutralCoreTests
{
    /// <summary>The core layers the rule covers.</summary>
    private static readonly System.Reflection.Assembly[] CoreLayers =
        [Layers.Domain, Layers.Application, Layers.Infrastructure, Layers.Api];

    /// <summary>No core type references a reserved plugin namespace.</summary>
    [Fact]
    public void CoreTypes_ReferenceNoReservedPluginNamespace()
    {
        var offenders = CoreLayers
            .SelectMany(layer => PluginNamespaceScan.Offenders(layer, ReservedPluginNamespaces.Segments, ReservedPluginNamespaces.Allowed))
            .ToList();

        offenders.Should().BeEmpty("a deployment plugin is reached through an SDK contract loaded at run time, never named by the core");
    }

    /// <summary>No core assembly references an assembly under a reserved plugin namespace.</summary>
    [Fact]
    public void CoreAssemblies_ReferenceNoReservedPluginAssembly()
    {
        var offenders = CoreLayers
            .SelectMany(layer => PluginNamespaceScan.ReservedReferences(layer, ReservedPluginNamespaces.Segments, ReservedPluginNamespaces.Allowed))
            .ToList();

        offenders.Should().BeEmpty("a plugin assembly is never linked into the core");
    }

    /// <summary>The same rule fails on a planted type that holds a reserved-namespace type, so the check is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedReference()
    {
        var offenders = PluginNamespaceScan.Offenders(
            typeof(PlantedCoreType).Assembly, ReservedPluginNamespaces.Segments, ReservedPluginNamespaces.Allowed);

        offenders.Should().Contain(line => line.StartsWith(typeof(PlantedCoreType).FullName!, StringComparison.Ordinal)
            && line.EndsWith(typeof(Innovayse.Plugins.Planted.PlantedPluginContract).FullName!, StringComparison.Ordinal));
    }

    /// <summary>The allow-list exempts a namespace: the planted reference passes once its prefix is listed.</summary>
    [Fact]
    public void Rule_PassesWhenTheNamespaceIsAllowListed()
    {
        var offenders = PluginNamespaceScan.Offenders(
            typeof(PlantedCoreType).Assembly, ReservedPluginNamespaces.Segments, ["Innovayse.Plugins"]);

        offenders.Should().NotContain(line => line.StartsWith(typeof(PlantedCoreType).FullName!, StringComparison.Ordinal));
    }

    /// <summary>The optional identity provider package is allow-listed while the other reserved segments are not.</summary>
    [Fact]
    public void IdentityProviderPackage_IsAllowListed()
    {
        PluginNamespaceScan.IsReserved("Innovayse.Auth.Authentication", ReservedPluginNamespaces.Segments, ReservedPluginNamespaces.Allowed)
            .Should().BeFalse();
        PluginNamespaceScan.IsReserved("Innovayse.Plugins.Anything", ReservedPluginNamespaces.Segments, ReservedPluginNamespaces.Allowed)
            .Should().BeTrue();
        PluginNamespaceScan.IsReserved("Innovayse.Hostpanel.Application.Plugins", ReservedPluginNamespaces.Segments, ReservedPluginNamespaces.Allowed)
            .Should().BeFalse();
    }
}
