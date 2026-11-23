using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><c>GET api/public/ssh-agent/bundle.tar.gz</c>: anonymous, stable, and only what an installer needs.</summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class GetSshAgentBundleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The bundle is served anonymously, is the same on every call and holds the installer and profiles but no tests.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Bundle_IsAnonymousStableAndHoldsInstallerProfilesAndNoTestsAsync()
    {
        var http = factory.CreateClient();

        var response = await http.GetAsync("/api/public/ssh-agent/bundle.tar.gz");
        var again = await http.GetByteArrayAsync("/api/public/ssh-agent/bundle.tar.gz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/gzip");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal(again);

        var entries = new List<(string Name, UnixFileMode Mode)>();
        using (var tar = new TarReader(new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress)))
        {
            while (tar.GetNextEntry() is { } entry)
            {
                entries.Add((entry.Name, entry.Mode));
            }
        }

        var names = entries.Select(e => e.Name.Split('/', 2)[1]).ToList();
        names.Should().Contain(["hostpanel-ssh-key-agent", "install.sh", "uninstall.sh", "profiles/cwp.conf", "profiles/generic-linux.conf", "providers/cwp/account-check", "README.md"]);
        names.Should().NotContain(n => n.StartsWith("tests/") || n.Contains("__pycache__"));
        entries.Single(e => e.Name.EndsWith("/install.sh")).Mode.Should().HaveFlag(UnixFileMode.UserExecute);
        entries.Single(e => e.Name.EndsWith("/README.md")).Mode.Should().NotHaveFlag(UnixFileMode.UserExecute);
    }

    /// <summary>A profile a loaded plugin ships is served as a bundle holding that profile and its helper, anonymously.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Bundle_OfAPluginsProfile_HoldsTheProfileAndItsHelpersAsync()
    {
        var registry = new FakePluginRegistry();
        registry.AddAgentProfile(SshAgentTestSeeds.Profile("panel-plugin", SshAgentTestSeeds.ProfileId, "[agent]\nprovider = examplepanel\n"));
        var http = HostOver(registry).CreateClient();

        var response = await http.GetAsync($"/api/public/ssh-agent/bundle.tar.gz?profile={SshAgentTestSeeds.ProfileId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var names = EntryNames(await response.Content.ReadAsByteArrayAsync());
        names.Should().Contain(["install.sh", "profiles/examplepanel.conf", $"providers/examplepanel/{SshAgentTestSeeds.HelperName}"]);
    }

    /// <summary>The host's own default profile is served without a plugin, and is the same bytes as the bundle with no profile asked for.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Bundle_OfTheDefaultProfile_IsTheBuiltInBundleAsync()
    {
        var http = factory.CreateClient();

        var plain = await http.GetByteArrayAsync("/api/public/ssh-agent/bundle.tar.gz");
        var named = await http.GetByteArrayAsync("/api/public/ssh-agent/bundle.tar.gz?profile=generic-linux");

        named.Should().Equal(plain);
    }

    /// <summary>A profile no loaded plugin ships and the agent does not embed answers 404 with its code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Bundle_OfAnUnknownProfile_IsNotFoundAsync()
    {
        var http = HostOver(new FakePluginRegistry()).CreateClient();

        var response = await http.GetAsync("/api/public/ssh-agent/bundle.tar.gz?profile=nobody-ships-this");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_AGENT_PROFILE_NOT_FOUND);
    }

    /// <summary>Builds a host whose loaded plugins come from the given fake.</summary>
    /// <param name="registry">The fake registry.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostOver(FakePluginRegistry registry)
    {
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPluginRegistry>(registry)));
    }

    /// <summary>Lists the paths of a bundle below its root folder.</summary>
    /// <param name="bytes">The <c>.tar.gz</c> bytes.</param>
    /// <returns>The paths.</returns>
    private static List<string> EntryNames(byte[] bytes)
    {
        var names = new List<string>();
        using var tar = new TarReader(new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress));
        while (tar.GetNextEntry() is { } entry)
        {
            names.Add(entry.Name.Split('/', 2)[1]);
        }

        return names;
    }
}
