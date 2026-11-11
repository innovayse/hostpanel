using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>The rules <see cref="PluginFrameOriginResolver.LinkOrigins"/> follows when it joins the two origin lists.</summary>
public sealed class PluginFrameOriginResolverLinkOriginsTests
{
    /// <summary>Builds a manifest that declares the given origins.</summary>
    /// <param name="origins">The <c>externalOrigins</c> the manifest declares.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest ManifestWith(params string[] origins)
    {
        var basis = PluginActivationTestSeeds.Plugin(
            "link-origins", global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, typeof(FakeAppPlugin)).Manifest;
        return new PluginManifest
        {
            Id = basis.Id,
            Name = basis.Name,
            Version = basis.Version,
            Author = basis.Author,
            Description = basis.Description,
            Type = basis.Type,
            Category = basis.Category,
            EntryPoint = basis.EntryPoint,
            SdkVersion = basis.SdkVersion,
            ExternalOrigins = origins,
        };
    }

    /// <summary>A plain-http frame origin (a development one) is not a place a link may point at.</summary>
    [Fact]
    public void LinkOrigins_DropsPlainHttpFrameOrigins()
    {
        var result = PluginFrameOriginResolver.LinkOrigins(
            ManifestWith("https://docs.example.org"), ["http://localhost:5173", "https://live.example"]);

        result.Should().Equal("https://docs.example.org", "https://live.example");
    }

    /// <summary>A wildcard origin of the manifest is kept as written, ahead of the caller's own origins.</summary>
    [Fact]
    public void LinkOrigins_KeepsWildcardManifestEntriesAsWritten()
    {
        var result = PluginFrameOriginResolver.LinkOrigins(ManifestWith("https://*.example.com"), ["https://live.example"]);

        result.Should().Equal("https://*.example.com", "https://live.example");
    }

    /// <summary>An origin named by both lists, or twice by one, appears once.</summary>
    [Fact]
    public void LinkOrigins_RemovesDuplicates()
    {
        var result = PluginFrameOriginResolver.LinkOrigins(
            ManifestWith("https://docs.example.org", "https://docs.example.org"),
            ["https://docs.example.org", "https://live.example", "https://live.example"]);

        result.Should().Equal("https://docs.example.org", "https://live.example");
    }

    /// <summary>With no frame origins (the admin panel) the manifest's origins are the whole answer.</summary>
    [Fact]
    public void LinkOrigins_WithNoFrameOrigins_AreTheManifestsOnly()
    {
        var result = PluginFrameOriginResolver.LinkOrigins(ManifestWith("https://docs.example.org"), []);

        result.Should().Equal("https://docs.example.org");
    }
}
