using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Outside Infrastructure and the <c>Providers.*</c> projects no type is named after an external vendor or
/// product: the core speaks in capabilities (payment gateway, registrar), never in whoever supplies one.
/// </summary>
public sealed partial class NoVendorNamesTests
{
    /// <summary>Matches a vendor or external product name anywhere in a type name, case-insensitively.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex("Stripe|Cwp|CPanel|Inecobank|Whmcs|Plesk|Namecheap|DirectAdmin|Hestia|PayPal|Cloudflare", RegexOptions.IgnoreCase)]
    private static partial Regex VendorName();

    /// <summary>No type in the kernel, domain, application, shared, API or SDK assemblies carries a vendor name.</summary>
    [Fact]
    public void CoreTypes_CarryNoVendorName()
    {
        var offenders = new[] { Layers.SharedKernel, Layers.Domain, Layers.Application, Layers.Shared, Layers.Api, Layers.Sdk }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => VendorName().IsMatch(type.Name))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("name the capability, and keep the vendor in Infrastructure or a Providers project");
    }

    /// <summary>
    /// Matches the text a user can read in C# source: raw, verbatim, regular and interpolated string literals,
    /// character literals (consumed so a quote character does not open a string), and <c>//</c>, <c>///</c> and block comments.
    /// Group <c>text</c> captures string literals and comments; identifiers match nothing.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"'(?:\\.|[^'\\])+'|(?<text>\$*""{3,}[\s\S]*?""{3,})|(?<text>[$@]{1,2}""(?:[^""]|"""")*"")|(?<text>""(?:[^""\\\r\n]|\\.)*"")|(?<text>//[^\r\n]*)|(?<text>/\*[\s\S]*?\*/)")]
    private static partial Regex ReadableText();

    /// <summary>
    /// No demo seed source file names a vendor in a string literal or a comment (regular, verbatim, interpolated and
    /// raw strings; <c>//</c>, <c>///</c> and block comments). Code identifiers are not scanned: vendor identifiers
    /// are legitimate inside Infrastructure, but the seed text a user sees names no outside product.
    /// </summary>
    [Fact]
    public void DemoSeedSources_CarryNoVendorName()
    {
        var backendRoot = UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);
        var demoRoot = Path.Combine(backendRoot, "src", "Innovayse.Hostpanel.Infrastructure", "Demo");

        var offenders = Directory
            .EnumerateFiles(demoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => ReadableText()
                .Matches(File.ReadAllText(path))
                .Any(match => match.Groups["text"].Success && VendorName().IsMatch(match.Groups["text"].Value)))
            .Select(path => Path.GetRelativePath(backendRoot, path).Replace('\\', '/'))
            .ToList();

        offenders.Should().BeEmpty("demo seed text names no outside product; use the capability name");
    }

    /// <summary>
    /// Matches a host-internal type or layer name: the SDK is a public package read by people who have never seen
    /// this repository, so its text names no class of the host, no layer of its architecture and no repository.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"ProvisioningProviderFactory|ServerConnectionTester|GetHostingPanelServerInfoHandler|CwpApiClient|Cwp7ApiClient|PluginActivator|PluginLoader|PluginRegistry|HostedPluginRunner|PluginManifestReader|Innovayse\.Hostpanel|(Application|Infrastructure|Domain|API) layer|this repository", RegexOptions.IgnoreCase)]
    private static partial Regex HostInternalName();

    /// <summary>
    /// No SDK source file or markdown document (README, CHANGELOG, CONTRIBUTING) names a vendor or a host-internal type in a comment or a string literal (regular, verbatim,
    /// interpolated and raw strings; <c>//</c>, <c>///</c> and block comments), so the public package documents the
    /// contract in generic terms only.
    /// </summary>
    [Fact]
    public void SdkSources_CarryNoVendorOrHostInternalName()
    {
        var backendRoot = UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);
        var sdkRoot = Path.Combine(backendRoot, "src", "Innovayse.SDK");
        var separators = new[] { Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar };

        var sources = Directory
            .EnumerateFiles(sdkRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !separators.Any(separator => path.Contains(separator, StringComparison.Ordinal)))
            .ToList();
        var documents = new[] { "README.md", "CHANGELOG.md", "CONTRIBUTING.md" }
            .Select(name => Path.Combine(sdkRoot, name))
            .Where(File.Exists)
            .ToList();

        sources.Should().NotBeEmpty("the scan must find the SDK sources, or it passes without reading anything");
        documents.Should().NotBeEmpty("the scan must find the SDK documents, or it passes without reading anything");

        var offenders = sources
            .SelectMany(path => ReadableText()
                .Matches(File.ReadAllText(path))
                .Where(match => match.Groups["text"].Success)
                .Where(match => VendorName().IsMatch(match.Groups["text"].Value) || HostInternalName().IsMatch(match.Groups["text"].Value))
                .Select(_ => Path.GetRelativePath(backendRoot, path).Replace('\\', '/')))
            .Concat(documents
                .Where(path => NamesOutsideProduct(File.ReadAllText(path)))
                .Select(path => Path.GetRelativePath(backendRoot, path).Replace('\\', '/')))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty("SDK comments and strings name the capability, never a vendor or a class of the host");
    }

    /// <summary>
    /// Whether SDK prose names a vendor or a host-internal type. The SDK documents live in the SDK's own repository,
    /// so "this repository" there means that repository and is not a leak; every other host name still is.
    /// </summary>
    /// <param name="text">The markdown document text.</param>
    /// <returns><see langword="true"/> when the text names a vendor or a host-internal type.</returns>
    private static bool NamesOutsideProduct(string text)
    {
        var prose = text.Replace("this repository", string.Empty, StringComparison.OrdinalIgnoreCase);
        return VendorName().IsMatch(prose) || HostInternalName().IsMatch(prose);
    }

    /// <summary>The scan has teeth: the host-internal expression matches the names the old comment used and nothing generic.</summary>
    [Fact]
    public void HostInternalName_MatchesHostNamesOnly()
    {
        HostInternalName().IsMatch("see CwpApiClient and ProvisioningProviderFactory").Should().BeTrue();
        HostInternalName().IsMatch("the Application layer applies the fallback").Should().BeTrue();
        HostInternalName().IsMatch("every other consumer in this repository").Should().BeTrue();
        HostInternalName().IsMatch("the host applies the fallback to a hosting panel client").Should().BeFalse();

        // The scan reads comments, so a name planted in a doc comment is found by it.
        ReadableText()
            .Matches("/// <c>CwpApiClient</c>\npublic sealed class Planted;")
            .Any(match => match.Groups["text"].Success && HostInternalName().IsMatch(match.Groups["text"].Value))
            .Should().BeTrue();
    }
}
