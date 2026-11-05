using System.Reflection;
using Hostpanel.Sdk.Packaging.Naming;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The plugin package rules (signature format and signing, plugin and page ids, versions, reserved assembly names, the
/// icon, archive and manifest checks) live once, in the plugin packaging package, which the host and a plugin author
/// both use. The host keeps its policy, never a second copy of a rule.
/// </summary>
public sealed class PluginPackagingRulesTests
{
    /// <summary>The names of the types the packaging package owns; a host assembly that declares one has copied a rule.</summary>
    private static readonly string[] PackagedTypeNames =
    [
        "PluginIdentifier", "PluginPageIdentifier", "PluginVersion", "PluginVersionChange", "PluginAssemblyNames",
        "HostAssemblyNames", "PluginSignatureFile", "PluginSignaturePayload", "PluginSigner", "PluginSignatureValidator",
        "TrustedKeyId", "PluginIconChecker", "PluginManifestChecker", "PluginArchiveGuard", "PluginRoutePath",
    ];

    /// <summary>The packaging package's assembly.</summary>
    private static readonly Assembly Packaging = typeof(PluginIdentifier).Assembly;

    /// <summary>No layer of the host declares a type named like one the packaging package owns.</summary>
    [Fact]
    public void NoHostLayerDeclaresATypeThePackagingPackageOwns()
    {
        var offenders = new[] { Layers.SharedKernel, Layers.Domain, Layers.Shared, Layers.Application, Layers.Infrastructure, Layers.Api }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => PackagedTypeNames.Contains(type.Name, StringComparer.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("the rule is in the packaging package; the host asks it and adds only its own policy");
    }

    /// <summary>The scan has teeth: the names it looks for are types the packaging package really declares.</summary>
    [Fact]
    public void EveryPackagedTypeName_IsATypeOfThePackagingPackage()
    {
        var declared = Layers.AuthoredTypesOf(Packaging).Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        PackagedTypeNames.Where(name => name != "HostAssemblyNames").Should().OnlyContain(name => declared.Contains(name));
    }

    /// <summary>The layers that ask the rules (application, infrastructure) reference the packaging package.</summary>
    [Fact]
    public void TheHostLayersThatAskTheRules_ReferenceThePackagingPackage()
    {
        var name = Packaging.GetName().Name;

        Layers.Infrastructure.GetReferencedAssemblies().Select(reference => reference.Name).Should().Contain(name);
        Layers.Application.GetReferencedAssemblies().Select(reference => reference.Name).Should().Contain(name);
    }

    /// <summary>The domain keeps to the kernel: it does not reference the packaging package, the use case validators check plugin ids.</summary>
    [Fact]
    public void TheDomain_DoesNotReferenceThePackagingPackage()
    {
        Layers.Domain.GetReferencedAssemblies().Select(reference => reference.Name).Should().NotContain(Packaging.GetName().Name);
    }
}
