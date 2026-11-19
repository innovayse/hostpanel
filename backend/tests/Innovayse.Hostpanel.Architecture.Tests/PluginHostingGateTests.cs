using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Every plugin host access to a client's hosting passes one gate (#1294): only <see cref="PluginHostingGate"/> takes the
/// plugin manifest lookup and the operator's slot bindings for host access, so a new kind of access cannot skip a check
/// or carry its own copy of one.
/// </summary>
public sealed class PluginHostingGateTests
{
    /// <summary>The namespace prefix of the Application services a plugin host access is built from.</summary>
    private const string ServicesNamespace = "Innovayse.Hostpanel.Application.Services";

    /// <summary>The namespace of the Infrastructure plugin adapters.</summary>
    private const string PluginsNamespace = "Innovayse.Hostpanel.Infrastructure.Plugins";

    /// <summary>The Infrastructure adapters that hand a host access to a plugin; the host API is internal, so it is found by name.</summary>
    private static readonly string[] AdapterNames = ["PluginHostApi", "HostPluginCallServices"];

    /// <summary>The ports only the gate may take for host access.</summary>
    private static readonly Type[] GatePorts = [typeof(IPluginSlotContextReader), typeof(IPluginEndpointResolver)];

    /// <summary>No host access type other than the gate takes the slot-context reader or the manifest lookup.</summary>
    [Fact]
    public void OnlyTheGate_TakesTheManifestLookupAndSlotBindings()
    {
        var offenders = HostAccessTypes()
            .Where(type => type != typeof(PluginHostingGate))
            .Where(type => TakesAny(type, GatePorts))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("host access checks live once in PluginHostingGate");
    }

    /// <summary>The gate itself takes both ports, which proves the rule above looks at the right constructors.</summary>
    [Fact]
    public void TheGate_TakesBothPorts()
    {
        GatePorts.All(port => TakesAny(typeof(PluginHostingGate), [port])).Should().BeTrue();
    }

    /// <summary>Every service that hands a plugin access to a client's hosting takes the gate, so none of them can skip a check.</summary>
    [Fact]
    public void EveryHostAccessService_TakesTheGate()
    {
        var services = new[] { typeof(PluginSshAccessService), typeof(PluginHostingService) };

        services.All(service => TakesAny(service, [typeof(PluginHostingGate)])).Should().BeTrue();
    }

    /// <summary>The Application's host access services are exactly the ones this rule knows, so a new one is added to the rule above.</summary>
    [Fact]
    public void HostAccessServices_AreTheKnownOnes()
    {
        var found = HostAccessTypes()
            .Where(type => type.Namespace == ServicesNamespace + ".Common" && type.Name.StartsWith("Plugin", StringComparison.Ordinal) && type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(type => type.Name);

        found.Should().BeEquivalentTo(nameof(PluginSshAccessService), nameof(PluginHostingService));
    }

    /// <summary>The types a host access is built from: the Application services and the host API adapters.</summary>
    /// <returns>Those types.</returns>
    private static IEnumerable<Type> HostAccessTypes()
    {
        var services = Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.Namespace is { } ns && ns.StartsWith(ServicesNamespace, StringComparison.Ordinal));
        var adapters = Layers.AuthoredTypesOf(Layers.Infrastructure)
            .Where(type => type.Namespace == PluginsNamespace && AdapterNames.Contains(type.Name));
        return services.Concat(adapters);
    }

    /// <summary>Whether a constructor of <paramref name="type"/> takes any of <paramref name="ports"/>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="ports">The port types.</param>
    /// <returns><see langword="true"/> when one of its constructors takes one of them.</returns>
    private static bool TakesAny(Type type, Type[] ports)
    {
        return type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => ports.Contains(parameter.ParameterType));
    }
}
