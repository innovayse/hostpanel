using System.Text.Json;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Infrastructure.Integrations.Migration;
using Innovayse.Hostpanel.Infrastructure.Integrations.Migration.Settings;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations;

/// <summary>
/// <see cref="MigrationSourceClient"/>: a source address this server may not call is reported as its
/// own failure, and no request reaches it. Private networks are refused unless the migration source
/// options allow them, whatever the shared outbound setting says.
/// </summary>
public sealed class MigrationSourceClientTests
{
    /// <summary>The key sent to the source; the stub never checks it.</summary>
    private const string AccessKey = "key";

    /// <summary>A source on loopback is refused at connect time, and the stub sees no request.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Ping_SourceOnLoopback_IsRefusedAsAddressNotAllowedAsync()
    {
        await using var stub = await StubHttpServer.StartAsync();
        stub.Enqueue("{}");
        var client = Client(new MigrationSourceOptions { AllowInsecureHttp = true });

        var act = () => client.PingAsync(new MigrationSourceEndpoint(stub.BaseAddress.ToString(), AccessKey), CancellationToken.None);

        await act.Should().ThrowAsync<MigrationSourceAddressRefusedException>();
        stub.Requests.Should().BeEmpty();
    }

    /// <summary>A plain HTTP source is refused before any connection while insecure HTTP is not allowed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Ping_PlainHttpSource_IsRefusedAsAddressNotAllowedAsync()
    {
        await using var stub = await StubHttpServer.StartAsync();
        stub.Enqueue("{}");
        var client = Client(new MigrationSourceOptions { AllowPrivateNetworks = true });

        var act = () => client.PingAsync(new MigrationSourceEndpoint(stub.BaseAddress.ToString(), AccessKey), CancellationToken.None);

        await act.Should().ThrowAsync<MigrationSourceAddressRefusedException>();
        stub.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A page request carries the keyset id beside the page number, in the plugin's camelCase
    /// members, and the id the plugin names for the next page is read back.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetRecordPage_SendsTheKeysetIdAndReadsTheNextOneAsync()
    {
        await using var stub = await StubHttpServer.StartAsync();
        stub.Enqueue("""{"items":[{"id":41}],"totalPages":3,"nextAfterId":90}""");

        // The connector refuses loopback whatever the options say, so this wire test sends through a
        // factory that delivers to the stub a request aimed at a public source address.
        var client = new MigrationSourceClient(
            new StubHttpClientFactory(stub), Options.Create(new MigrationSourceOptions()), NullLogger<MigrationSourceClient>.Instance);

        var page = await client.GetRecordPageAsync<JsonElement>(
            new MigrationSourceEndpoint("https://billing.example.test/", AccessKey),
            new MigrationPageSelection("clients", 2, 50, 40),
            CancellationToken.None);

        page.NextAfterId.Should().Be(90);
        page.Items.Should().ContainSingle();
        using var sent = JsonDocument.Parse(stub.Requests.Should().ContainSingle().Subject.Body);
        sent.RootElement.GetProperty("key").GetString().Should().Be(AccessKey);
        sent.RootElement.GetProperty("action").GetString().Should().Be("clients");
        sent.RootElement.GetProperty("page").GetInt32().Should().Be(2);
        sent.RootElement.GetProperty("perPage").GetInt32().Should().Be(50);
        sent.RootElement.GetProperty("afterId").GetInt64().Should().Be(40);
    }

    /// <summary>The migration source refuses private networks unless its own option allows them.</summary>
    [Fact]
    public void Options_RefusePrivateNetworksByDefault()
    {
        new MigrationSourceOptions().AllowPrivateNetworks.Should().BeFalse();
    }

    /// <summary>Builds the client over a connector that follows the migration source's private network setting.</summary>
    /// <param name="options">The migration source options.</param>
    /// <returns>The client under test.</returns>
    private static MigrationSourceClient Client(MigrationSourceOptions options)
    {
        var connector = new OutboundConnector(
            Options.Create(new OutboundNetworkOptions { AllowPrivateNetworks = options.AllowPrivateNetworks }),
            Options.Create(new PanelNetworkOptions()));
        return new MigrationSourceClient(
            new ConnectorHttpClientFactory(connector), Options.Create(options), NullLogger<MigrationSourceClient>.Instance);
    }
}
