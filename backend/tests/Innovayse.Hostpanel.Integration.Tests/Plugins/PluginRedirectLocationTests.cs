using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The rules <see cref="PluginRedirectLocation"/> holds a callback's redirect address to: a path on this host or an
/// absolute <c>https</c> address of an allowed origin, with the same path rules for both, and nothing that a header, a
/// browser or a proxy could read as another address than the one the host checked.
/// </summary>
public sealed class PluginRedirectLocationTests
{
    /// <summary>The origin of the client portal in these tests.</summary>
    private const string Portal = "https://portal.example";

    /// <summary>An origin a manifest declares.</summary>
    private const string Gateway = "https://pay.gateway.example";

    /// <summary>The origins an absolute address may name in these tests.</summary>
    private static readonly string[] Allowed = [Portal, Gateway];

    /// <summary>Builds a manifest that declares the given origins.</summary>
    /// <param name="origins">The <c>externalOrigins</c> the manifest declares.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest ManifestWith(params string[] origins)
    {
        var basis = PluginActivationTestSeeds.Plugin(
            "redirect-origins", global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, typeof(FakeAppPlugin)).Manifest;
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

    /// <summary>A path on this host, with or without a query, and an address of an allowed origin pass, and are written as they came.</summary>
    /// <param name="location">The address the plugin gave.</param>
    /// <param name="expected">The address the host sends.</param>
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/payment/result", "/payment/result")]
    [InlineData("/payment/result?x=1&y=a%20b", "/payment/result?x=1&y=a%20b")]
    [InlineData("/apiary/x", "/apiary/x")]
    [InlineData("/a/b..c/d", "/a/b..c/d")]
    [InlineData("https://portal.example/payment/result?x=1", "https://portal.example/payment/result?x=1")]
    [InlineData("https://pay.gateway.example/done", "https://pay.gateway.example/done")]
    [InlineData("https://pay.gateway.example", "https://pay.gateway.example")]
    [InlineData("https://pay.gateway.example?x=1", "https://pay.gateway.example?x=1")]
    [InlineData("HTTPS://PAY.GATEWAY.EXAMPLE/Done", "https://pay.gateway.example/Done")]
    public void AnAcceptableAddress_PassesAsItCame(string location, string expected)
    {
        var accepted = PluginRedirectLocation.TryNormalize(location, Allowed, out var safe);

        accepted.Should().BeTrue();
        safe.Should().Be(expected);
    }

    /// <summary>Every shape of address the rules refuse, each of which a plugin may still build when it does not use the SDK factory or builds the address from the request.</summary>
    /// <param name="location">The address the plugin gave.</param>
    [Theory]
    [InlineData("")]
    [InlineData("payment/result")]
    [InlineData("?x=1")]
    [InlineData("//evil.test")]
    [InlineData("//evil.test/x")]
    [InlineData("/\\evil.test")]
    [InlineData("\\\\evil.test")]
    [InlineData("/%2f/evil.test")]
    [InlineData("/ok/%2F/x")]
    [InlineData("/%5cevil.test")]
    [InlineData("/ok/%5C/x")]
    [InlineData("/./x")]
    [InlineData("/ok/./x")]
    [InlineData("/../x")]
    [InlineData("/ok/../x")]
    [InlineData("/%2e/x")]
    [InlineData("/%2e%2e/x")]
    [InlineData("/%2E%2E/api")]
    [InlineData("/api")]
    [InlineData("/api/")]
    [InlineData("/api/x")]
    [InlineData("/API/x")]
    [InlineData("/Api?x=1")]
    [InlineData("/%61pi/x")]
    [InlineData("/ok#frag")]
    [InlineData("/ok\r\nSet-Cookie: a=b")]
    [InlineData("/ok\nx")]
    [InlineData("/ok\tx")]
    [InlineData("/ok\0x")]
    [InlineData("/ok\u007fx")]
    [InlineData("/ok\u0085x")]
    [InlineData("/ok x")]
    [InlineData(" /ok")]
    [InlineData("/ok ")]
    [InlineData("/oké")]
    [InlineData("http://pay.gateway.example/x")]
    [InlineData("ftp://pay.gateway.example/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("https://evil.example/x")]
    [InlineData("https://pay.gateway.example.evil.example/x")]
    [InlineData("https://sub.pay.gateway.example/x")]
    [InlineData("https://user@pay.gateway.example/x")]
    [InlineData("https://user:pass@pay.gateway.example/x")]
    [InlineData("https://pay.gateway.example@evil.example/x")]
    [InlineData("https://pay.gateway.example:8443/x")]
    [InlineData("https://pay.gateway.example:443/x")]
    [InlineData("https://pay.gateway.example//evil.test")]
    [InlineData("https://pay.gateway.example/%2e%2e/x")]
    [InlineData("https://pay.gateway.example/api/x")]
    [InlineData("https://pay.gateway.example/ok#frag")]
    [InlineData("https:///x")]
    [InlineData("https:/pay.gateway.example/x")]
    public void AnUnacceptableAddress_IsRefused(string location)
    {
        var accepted = PluginRedirectLocation.TryNormalize(location, Allowed, out var safe);

        accepted.Should().BeFalse();
        safe.Should().BeNull();
    }

    /// <summary>An address of exactly the longest allowed length passes, and one character more is refused.</summary>
    [Fact]
    public void TheLengthCap_IsExact()
    {
        var longest = "/" + new string('a', PluginRedirectLocation.MaxLength - 1);

        PluginRedirectLocation.TryNormalize(longest, Allowed, out _).Should().BeTrue();
        PluginRedirectLocation.TryNormalize(longest + "a", Allowed, out _).Should().BeFalse();
    }

    /// <summary>A missing address and an empty allow list refuse every absolute address while a path still passes.</summary>
    [Fact]
    public void WithNoAllowedOrigin_OnlyAPathPasses()
    {
        PluginRedirectLocation.TryNormalize(null, Allowed, out _).Should().BeFalse();
        PluginRedirectLocation.TryNormalize("https://portal.example/x", [], out _).Should().BeFalse();
        PluginRedirectLocation.TryNormalize("/x", [], out _).Should().BeTrue();
    }

    /// <summary>The allowed origins are the manifest's pure https origins and the portal's own, each once.</summary>
    [Fact]
    public void AllowedOrigins_AreTheManifestsAndThePortals()
    {
        var manifest = ManifestWith(Gateway, "https://*.wildcard.example", "http://plain.example", "https://portal.example");

        var origins = PluginRedirectLocation.AllowedOrigins(manifest, "https://portal.example/some/base");

        origins.Should().Equal(Gateway, Portal);
    }

    /// <summary>A portal that is not served over https names no absolute origin (a developer's own machine), and a manifest alone still does.</summary>
    [Fact]
    public void APortalOverPlainHttp_IsNotAnAllowedOrigin()
    {
        var origins = PluginRedirectLocation.AllowedOrigins(ManifestWith(Gateway), "http://localhost:3000");

        origins.Should().Equal(Gateway);
    }

    /// <summary>A portal address that does not parse names no origin and does not fail the call.</summary>
    [Fact]
    public void APortalAddressThatDoesNotParse_NamesNoOrigin()
    {
        PluginRedirectLocation.AllowedOrigins(ManifestWith(), "not a url").Should().BeEmpty();
    }
}
