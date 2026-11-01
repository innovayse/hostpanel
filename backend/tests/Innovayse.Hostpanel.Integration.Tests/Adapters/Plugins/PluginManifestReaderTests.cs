using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>How the host reads the optional <c>capabilities</c> list of a <c>plugin.json</c>.</summary>
public sealed class PluginManifestReaderTests
{
    /// <summary>Builds a manifest text with the given extra members after the required ones.</summary>
    /// <param name="extra">Extra JSON members, for example <c>,"capabilities":null</c>, or an empty string.</param>
    /// <returns>The manifest text.</returns>
    private static string Manifest(string extra)
    {
        return "{\"id\":\"p\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + "\"type\":\"payment\",\"category\":\"Payment\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"1.0\"" + extra + "}";
    }

    /// <summary>A manifest without <c>capabilities</c> reads as an empty list.</summary>
    [Fact]
    public void Deserialize_CapabilitiesAbsent_IsEmpty()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(string.Empty));

        manifest!.Capabilities.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>A manifest with <c>"capabilities": null</c> reads as an empty list, never null.</summary>
    [Fact]
    public void Deserialize_CapabilitiesNull_IsEmpty()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(",\"capabilities\":null"));

        manifest!.Capabilities.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>A manifest that lists capabilities keeps them in order.</summary>
    [Fact]
    public void Deserialize_CapabilitiesPresent_KeepsThem()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(",\"capabilities\":[\"storedCredentials\"]"));

        manifest!.Capabilities.Should().Equal(PluginCapabilities.StoredCredentials);
    }

    /// <summary>The file reader returns the manifest with an empty list when <c>capabilities</c> is null.</summary>
    [Fact]
    public void TryRead_CapabilitiesNull_ReturnsManifestWithEmptyList()
    {
        var path = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, Manifest(",\"capabilities\":null"));
        try
        {
            var manifest = new PluginManifestReader(NullLogger<PluginManifestReader>.Instance).TryRead(path);

            manifest.Should().NotBeNull();
            manifest!.Capabilities.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
