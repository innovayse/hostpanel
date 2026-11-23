using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><c>GET api/admin/servers/{id}/ssh-setup</c>: the values the one-command installer is built from.</summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class GetServerSshSetupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The setup names the profile, the management user, the bundle whose hash it verifies and the source addresses.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Setup_NamesTheProfileAndTheBundleItsHashVerifiesAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "setup.example.test", 22, enabled: false);

        var setup = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-setup");

        setup.GetProperty("profile").GetString().Should().Be(SshTestSeeds.BuiltInProfile);
        setup.GetProperty("agentUsername").GetString().Should().Be("hpsshkeys");
        setup.GetProperty("bundleVersion").GetString().Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        var bytes = await factory.CreateClient().GetByteArrayAsync(setup.GetProperty("bundlePath").GetString());
        Convert.ToHexStringLower(SHA256.HashData(bytes)).Should().Be(setup.GetProperty("bundleSha256").GetString());
        setup.GetProperty("sourceAddresses").EnumerateArray().Select(a => a.GetString())
            .Should().Equal("203.0.113.10", "2001:db8::10");
    }

    /// <summary>A server whose module has no key agent has no profile.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnsupportedModule_HasNoProfileAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "da.example.test", 22, ServerModules.CPanel, enabled: false);

        var setup = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-setup");

        setup.GetProperty("profile").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>An unknown server answers 404 <c>SERVER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownServer_Returns404Async()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);

        var response = await admin.GetAsync("/api/admin/servers/2147483000/ssh-setup");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SERVER_NOT_FOUND);
    }
}
