using System.Reflection;
using Hostpanel.Sdk.Packaging.Naming;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// <c>Innovayse.Hostpanel.Plugins.</c> is the namespace of plugin assemblies and the one carve-out of the reserved
/// <c>Innovayse.Hostpanel.</c> host prefix, so no host assembly or project may be named under it: otherwise a plugin
/// package could carry a DLL that shadows a host assembly.
/// </summary>
public sealed class PluginAssemblyNamespaceTests
{
    /// <summary>The name of the host type that holds the assembly name options, in the infrastructure assembly.</summary>
    private const string HostAssembliesTypeName = "Innovayse.Hostpanel.Infrastructure.Plugins.PluginHostAssemblies";

    /// <summary>
    /// The carve-out prefix, read from the host's own <c>PluginHostAssemblies.Options</c> (the one place the host writes
    /// it), not repeated here, so this test can never pass against a different prefix than the one the install checks use.
    /// </summary>
    private static readonly string Prefix = ReadHostOptions().PluginNamespacePrefix;

    /// <summary>The host prefix, read from the same place.</summary>
    private static readonly string HostPrefix = ReadHostOptions().HostPrefix;

    /// <summary>The host's options hold the prefixes this test relies on: the plugin namespace sits under the host prefix, both end in a dot.</summary>
    [Fact]
    public void HostOptions_AreWellFormed()
    {
        HostPrefix.Should().EndWith(".");
        Prefix.Should().EndWith(".");
        Prefix.Should().StartWith(HostPrefix).And.NotBe(HostPrefix);
        PluginAssemblyNames.IsPluginNamespaceName(Prefix + "Stripe", ReadHostOptions()).Should().BeTrue();
        PluginAssemblyNames.IsPluginNamespaceName(Prefix.TrimEnd('.'), ReadHostOptions()).Should().BeFalse();
    }

    /// <summary>No layer assembly or assembly it references is named under the plugin namespace.</summary>
    [Fact]
    public void NoHostAssemblyIsNamedUnderThePluginNamespace()
    {
        Assembly[] layers =
        [
            Layers.SharedKernel, Layers.Domain, Layers.Shared, Layers.Application, Layers.Infrastructure,
            Layers.Api, Layers.Sdk, Layers.ProviderCwp, Layers.ProviderCwp7,
        ];
        var names = layers
            .Select(assembly => assembly.GetName().Name!)
            .Concat(layers.SelectMany(assembly => assembly.GetReferencedAssemblies()).Select(name => name.Name!))
            .Distinct()
            .ToList();

        names.Where(name => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)).Should().BeEmpty(
            "a host assembly under the plugin namespace would let a plugin package shadow it");
    }

    /// <summary>No project in the backend tree (source, tools or tests) is named under the plugin namespace.</summary>
    [Fact]
    public void NoProjectIsNamedUnderThePluginNamespace()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Innovayse.Hostpanel.sln")))
        {
            root = root.Parent;
        }

        root.Should().NotBeNull("the backend solution is found above the test output");
        var offenders = Directory.EnumerateFiles(root!.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        offenders.Should().BeEmpty("project names under the plugin namespace belong to plugins, never to the host");
    }

    /// <summary>Reads <c>PluginHostAssemblies.Options</c> from the infrastructure assembly, whose type is internal.</summary>
    /// <returns>The options the host's install and load checks use.</returns>
    private static PluginAssemblyOptions ReadHostOptions()
    {
        var type = Layers.Infrastructure.GetType(HostAssembliesTypeName, throwOnError: true)!;
        var property = type.GetProperty("Options", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        property.Should().NotBeNull("the host keeps its assembly name options in one static member");

        return (PluginAssemblyOptions)property!.GetValue(null)!;
    }
}
