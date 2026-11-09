using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The host's icon adapter over the plugin packaging package's icon checker: a valid SVG and PNG are returned as the
/// host's icon DTO with their type and tag, a missing file and an unacceptable path map to the host's manifest rules.
/// The checks themselves (size, PNG header, hostile SVG, links) are the package's own tests. Temp folders only; no
/// container or database.
/// </summary>
public sealed class PluginIconReaderTests : IDisposable
{
    /// <summary>The root of a harmless SVG icon.</summary>
    private const string SvgOpen = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\">";

    /// <summary>A minimal valid PNG start: the signature, a 13-byte IHDR length and the IHDR type.</summary>
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01,
    ];

    /// <summary>A scratch plugin folder per test instance, removed on disposal.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"icon-rules-{Guid.NewGuid():N}");

    /// <summary>Creates the scratch folder.</summary>
    public PluginIconReaderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Reads the icon a manifest naming <paramref name="icon"/> declares, from the scratch folder.</summary>
    /// <param name="icon">The manifest <c>icon</c> value, or <see langword="null"/> for a manifest without one.</param>
    /// <returns>The read outcome.</returns>
    private PluginIconRead Read(string? icon)
    {
        var member = icon is null ? string.Empty : $",\"icon\":\"{icon}\"";
        var manifest = PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("icon-plugin", member))!;
        return PluginIconReader.Read(_folder, manifest);
    }

    /// <summary>Writes a file under the scratch folder.</summary>
    /// <param name="path">The package-relative path.</param>
    /// <param name="content">The file bytes.</param>
    private void Write(string path, byte[] content)
    {
        var full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
    }

    /// <summary>Asserts that reading breaks one given rule and yields no icon.</summary>
    /// <param name="read">The outcome.</param>
    /// <param name="rule">The rule it must break.</param>
    private static void AssertBreaks(PluginIconRead read, PluginManifestRule rule)
    {
        read.Violation.Should().NotBeNull();
        read.Violation!.Rule.Should().Be(rule);
        read.Icon.Should().BeNull();
    }

    /// <summary>A manifest without an icon reads as no icon and no violation.</summary>
    [Fact]
    public void NoIcon_IsNeitherAnIconNorAViolation()
    {
        var read = Read(null);

        read.Icon.Should().BeNull();
        read.Violation.Should().BeNull();
    }

    /// <summary>A plain SVG is read with the SVG type and a quoted strong tag.</summary>
    [Fact]
    public void ValidSvg_IsReadWithItsTypeAndTag()
    {
        Write("assets/icon.svg", Encoding.UTF8.GetBytes($"{SvgOpen}<defs><linearGradient id=\"g\"/></defs><path fill=\"url(#g)\" d=\"M0 0h8v8z\"/></svg>"));

        var read = Read("assets/icon.svg");

        read.Violation.Should().BeNull();
        read.Icon!.ContentType.Should().Be("image/svg+xml");
        read.Icon.ETag.Should().StartWith("\"").And.EndWith("\"");
        read.Icon.Content.Should().NotBeEmpty();
    }

    /// <summary>A real PNG is read with the PNG type.</summary>
    [Fact]
    public void ValidPng_IsReadWithItsType()
    {
        Write("icon.png", PngBytes);

        var read = Read("icon.png");

        read.Violation.Should().BeNull();
        read.Icon!.ContentType.Should().Be("image/png");
        read.Icon.Content.Should().Equal(PngBytes);
    }

    /// <summary>A path that leaves the package, is absolute or has a wrong extension is refused as invalid.</summary>
    /// <param name="icon">The manifest value.</param>
    [Theory]
    [InlineData("../icon.svg")]
    [InlineData("assets/../../icon.svg")]
    [InlineData("/etc/icon.svg")]
    [InlineData("C:/icon.svg")]
    [InlineData("assets\\\\icon.svg")]
    [InlineData("icon.jpg")]
    [InlineData("icon.svgz")]
    [InlineData("icon.SVG")]
    public void UnsafePath_IsRefused(string icon)
    {
        Write("icon.svg", Encoding.UTF8.GetBytes($"{SvgOpen}</svg>"));

        AssertBreaks(Read(icon), PluginManifestRule.IconInvalid);
    }

    /// <summary>An icon the package does not hold is refused as missing.</summary>
    [Fact]
    public void MissingFile_IsRefusedAsMissing()
    {
        AssertBreaks(Read("icon.svg"), PluginManifestRule.IconMissing);
    }
}
