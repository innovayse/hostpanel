using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/registry/{id}/install</c> for a plugin only a verified installation may have, against a fake registry
/// the host reaches over its real clients: the installation registers and is confirmed on demand, the version details, the version
/// list, the ticket and the package fetch are signed and carry no licence key, the checksum and signature rules still apply, and
/// every refusal of the registry reaches the admin as a code with a sentence in his language.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InstallRestrictedRegistryPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of a registry install.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string Route(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/install";

    /// <summary>A restricted plugin offered by a fresh fake registry.</summary>
    /// <param name="registry">The fake registry.</param>
    /// <param name="publishedSha256">The checksum to publish; the real one when <see langword="null"/>.</param>
    /// <returns>The plugin id.</returns>
    private static string Offer(FakeRegistryServer registry, string? publishedSha256 = null)
    {
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Offer(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), hostOnly: true, publishedSha256);
        return pluginId;
    }

    /// <summary>Posts an install and reads the answer.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="language">The <c>X-Language</c> value; none when <see langword="null"/>.</param>
    /// <returns>The status and the JSON body.</returns>
    private static async Task<(HttpStatusCode Status, JsonElement Body)> InstallAsync(HttpClient client, string pluginId, string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route(pluginId))
        {
            Content = JsonContent.Create(PluginRegistryTestSeeds.InstallBody("1.0.0")),
        };
        if (language is not null)
        {
            request.Headers.Add("X-Language", language);
        }

        var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    /// <summary>
    /// A restricted plugin installs from a host that was never registered: it registers on demand, then the details, the list, the
    /// ticket and the package fetch are signed, none carries the licence key that is stored for the plugin, and the install is audited.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARestrictedPlugin_RegistersOnDemand_AndInstallsThroughATicketAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);

        try
        {
            (await client.PutAsJsonAsync($"/api/admin/plugins/registry/{pluginId}/licence", new { key = "LICENCE-KEY-for-a-public-plugin" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (status, body) = await InstallAsync(client, pluginId);

            status.Should().Be(HttpStatusCode.OK);
            body.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();
            File.Exists(Path.Combine(PluginsTestSeeds.InstalledFolder(factory, pluginId), "readme.txt")).Should().BeTrue();
            registry.Instances.Single().Verified.Should().BeTrue();
            var signed = registry.Calls.Where(c => c.IsSigned).Select(c => (c.Method, Path: c.Target.Split('?')[0])).ToList();
            signed.Should().Equal(
                ("GET", $"/v1/plugins/{pluginId}/versions/1.0.0"),
                ("GET", $"/v1/plugins/{pluginId}/versions"),
                ("POST", $"/v1/plugins/{pluginId}/versions/1.0.0/download-tickets"),
                ("GET", $"/v1/plugins/{pluginId}/versions/1.0.0/download"));
            registry.Calls.Where(c => c.IsSigned).Should().OnlyContain(c => !c.HasLicenceKey);
            registry.Calls.Select(c => c.Target).Should().ContainInOrder(
                "/v1/instances", $"/v1/instances/{registry.Instances.Single().Id:D}/verify", $"/v1/plugins/{pluginId}/versions/1.0.0");

            using var scope = factory.Services.CreateScope();
            var line = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
                .SingleAsync(l => l.Description.Contains(pluginId) && l.Description.StartsWith("Installed Plugin"));
            line.Description.Should().StartWith("Installed Plugin From Registry").And.Contain($"Plugin ID: {pluginId} - Version: 1.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A second restricted install finds the installation verified and does not register it again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASecondRestrictedInstall_DoesNotRegisterAgainAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var first = Offer(registry);
        var second = Offer(registry);

        try
        {
            (await InstallAsync(client, first)).Status.Should().Be(HttpStatusCode.OK);
            (await InstallAsync(client, second)).Status.Should().Be(HttpStatusCode.OK);

            registry.Instances.Should().HaveCount(1);
            registry.Calls.Count(c => c.Target == "/v1/instances").Should().Be(1);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{first}");
            await client.DeleteAsync($"/api/admin/plugins/{second}");
        }
    }

    /// <summary>The checksum rule still applies to a restricted package: a package that does not hash to the published value is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARestrictedPackage_WithTheWrongChecksum_IsRefusedAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry, publishedSha256: new string('a', 64));

        var (status, body) = await InstallAsync(client, pluginId);

        status.Should().Be(HttpStatusCode.BadGateway);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_CHECKSUM_MISMATCH);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(factory, pluginId)).Should().BeFalse();
    }

    /// <summary>An installation that cannot be confirmed gets the code of the cause, and no signed call is made.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnInstallationThatCannotBeConfirmed_IsToldWhy_AndNoSignedCallIsMadeAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);
        registry.FetchChallenge = _ => Task.FromResult<string?>(null);

        var (status, body) = await InstallAsync(client, pluginId);

        status.Should().Be(HttpStatusCode.BadGateway);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE);
        registry.Calls.Should().NotContain(c => c.IsSigned);
    }

    /// <summary>A restricted plugin on a host whose registry is off is refused like any registry call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhileTheRegistryIsOff_TheInstallIsRefusedAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, enabled: false);
        var client = await host.AdminClientAsync(factory);

        var (status, body) = await InstallAsync(client, Offer(registry));

        status.Should().Be(HttpStatusCode.Conflict);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        registry.Calls.Should().BeEmpty();
    }

    /// <summary>Each refusal of a signed call reaches the admin with its status and code, and no text of the registry.</summary>
    /// <param name="registryStatus">The status the registry answers with.</param>
    /// <param name="registryCode">The registry error code.</param>
    /// <param name="httpStatus">The status the admin API answers with.</param>
    /// <param name="code">The host code.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(401, "INSTANCE_SIGNATURE_INVALID_UNAUTHORIZED", 502, "PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED")]
    [InlineData(401, "INSTANCE_TIMESTAMP_UNAUTHORIZED", 502, "PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW")]
    [InlineData(401, "INSTANCE_REPLAY_UNAUTHORIZED", 502, "PLUGIN_REGISTRY_INSTANCE_REPLAY_REJECTED")]
    [InlineData(403, "INSTANCE_NOT_VERIFIED_FORBIDDEN", 409, "PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED")]
    [InlineData(403, "INSTANCE_REVOKED_FORBIDDEN", 409, "PLUGIN_REGISTRY_INSTANCE_REVOKED")]
    [InlineData(429, "INSTANCE_RATE_LIMIT_EXCEEDED", 429, "PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED")]
    public async Task ARefusalOfASignedCall_ReachesTheAdminAsAClearCodeAsync(int registryStatus, string registryCode, int httpStatus, string code)
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);
        registry.Intercept = call => call.IsSigned && call.Target.EndsWith("/versions/1.0.0", StringComparison.Ordinal)
            ? new HttpResponseMessage((HttpStatusCode)registryStatus)
            {
                Content = new StringContent($$"""{"type":"{{registryCode}}","detail":"registry internal text"}""", Encoding.UTF8, "application/json"),
            }
            : null;

        var (status, body) = await InstallAsync(client, pluginId);

        ((int)status).Should().Be(httpStatus);
        body.GetProperty("type").GetString().Should().Be(code);
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace().And.NotContain("registry internal text");
    }

    /// <summary>The ticket the registry refuses becomes its own code with a sentence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARefusedTicket_ReachesTheAdminAsAClearCodeAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);
        registry.Intercept = call => call.Method == "GET" && call.Target.Contains("?ticket=")
            ? new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"type":"DOWNLOAD_TICKET_INVALID_FORBIDDEN"}""", Encoding.UTF8, "application/json"),
            }
            : null;

        var (status, body) = await InstallAsync(client, pluginId);

        status.Should().Be(HttpStatusCode.BadGateway);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED);
    }

    /// <summary>The clock refusal tells the admin to fix the server clock, in English, Russian and Armenian.</summary>
    /// <param name="language">The <c>X-Language</c> value.</param>
    /// <param name="fragment">A fragment of the sentence in that language.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("en", "Fix the server clock")]
    [InlineData("ru", "Исправьте время на сервере")]
    [InlineData("hy", "Ուղղեք սերվերի ժամը")]
    public async Task TheClockRefusal_TellsTheAdminToFixTheServerClock_InHisLanguageAsync(string language, string fragment)
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);
        registry.ClockOffset = TimeSpan.FromMinutes(30);

        var (status, body) = await InstallAsync(client, pluginId, language);

        status.Should().Be(HttpStatusCode.BadGateway);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW);
        body.GetProperty("detail").GetString().Should().Contain(fragment);
    }

    /// <summary>The details page of a restricted plugin lists its versions through a signed call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheDetailsPage_OfARestrictedPlugin_ListsItsVersionsThroughASignedCallAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);

        var response = await client.GetAsync($"/api/admin/plugins/registry/{pluginId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("versions").GetArrayLength().Should().Be(1);
        registry.Calls.Single(c => c.Target == $"/v1/plugins/{pluginId}/versions").IsSigned.Should().BeTrue();
        registry.Calls.Where(c => c.IsSigned).Should().OnlyContain(c => !c.HasLicenceKey);
    }

    /// <summary>The update check is the unsigned lookup and a restricted plugin with no package address in it is not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheUpdateCheck_IsUnsigned_AndARestrictedPluginInItIsNotAnErrorAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await host.AdminClientAsync(factory);
        var pluginId = Offer(registry);

        try
        {
            (await InstallAsync(client, pluginId)).Status.Should().Be(HttpStatusCode.OK);
            var signedBefore = registry.Calls.Count(c => c.IsSigned);

            var response = await client.GetAsync("/api/admin/plugins/registry/updates");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            registry.Calls.Last(c => c.Target == "/v1/plugins/latest").IsSigned.Should().BeFalse();
            registry.Calls.Count(c => c.IsSigned).Should().Be(signedBefore);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }
}
