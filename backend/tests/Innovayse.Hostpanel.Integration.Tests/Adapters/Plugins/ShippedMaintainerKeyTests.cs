using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The maintainer key shipped in <c>appsettings.json</c> is the key official plugins are signed with, so a stock host
/// trusts the packages the plugin registry serves without the operator adding anything.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, whose options are bound from the shipped settings.</param>
public sealed class ShippedMaintainerKeyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The key id official plugins carry in their <c>plugin.sig</c>.</summary>
    private const string OfficialKeyId = "official-innovayse-2026";

    /// <summary>The base64 of the 32-byte Ed25519 public key that belongs to <see cref="OfficialKeyId"/>.</summary>
    private const string OfficialPublicKey = "j2rBL9ml72UXJWaqLBK6UafGJiV3XrLz3KhM4dexSM0=";

    /// <summary>The shipped maintainer list holds the official key with its public half, and nothing else.</summary>
    [Fact]
    public void TheShippedMaintainerKey_IsTheOfficialPluginKey()
    {
        var options = factory.Services.GetRequiredService<IOptions<PluginSigningOptions>>().Value;

        options.MaintainerKeys.Should().ContainSingle().Which.Should().BeEquivalentTo(new TrustedPluginKey
        {
            KeyId = OfficialKeyId,
            PublicKey = OfficialPublicKey,
        });
    }
}
