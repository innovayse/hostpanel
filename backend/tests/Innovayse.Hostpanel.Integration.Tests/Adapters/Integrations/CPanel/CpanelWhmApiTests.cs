using System.Globalization;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Resilience;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// Tests for <see cref="CpanelWhmApi"/> against a stub WHM server: authorization, the account
/// listing and the usage figures WHM sends as numbers or as strings.
/// </summary>
public sealed class CpanelWhmApiTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the WHM host.</summary>
    private StubHttpServer whm = null!;

    /// <summary>Starts the stub WHM server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        whm = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub WHM server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await whm.DisposeAsync();
    }

    /// <summary>Listing parses numeric and string figures, and reads "unlimited" as zero.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListAccounts_ParsesNumbersStringsAndUnlimitedAsync()
    {
        whm.Enqueue(
            """
            { "data": { "acct": [
              { "user": "a", "domain": "a.test", "owner": "root",
                "diskused": "12.5", "disklimit": "unlimited", "bwused": 3, "bwlimit": "100" },
              { "user": "b", "domain": "b.test", "owner": "root",
                "diskused": "0", "disklimit": "500", "bwused": "n/a", "bwlimit": "unlimited" }
            ] } }
            """);
        var api = CpanelWhmApi.Create("whm.test", "root", "token", new StubHttpClientFactory(whm));

        var usage = await api.ListAccountsAsync(CancellationToken.None);

        usage.Should().Equal(
            new WhmAccountUsage("a", "a.test", "root", 12, 0, 3, 100),
            new WhmAccountUsage("b", "b.test", "root", 0, 500, 0, 0));
    }

    /// <summary>A decimal point is read the same whatever culture the process runs in.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListAccounts_ReadsADecimalPointInAnyCultureAsync()
    {
        whm.Enqueue("""{ "data": { "acct": [ { "user": "a", "diskused": "1.5" } ] } }""");
        var api = CpanelWhmApi.Create("whm.test", "root", "token", new StubHttpClientFactory(whm));
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

        try
        {
            var usage = await api.ListAccountsAsync(CancellationToken.None);

            usage.Should().ContainSingle().Which.DiskUsedMb.Should().Be(1);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>A body with no data section lists nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListAccounts_ReturnsNothing_WhenTheBodyHasNoAccountsAsync()
    {
        whm.Enqueue("""{ "metadata": { "result": 1 } }""");
        var api = CpanelWhmApi.Create("whm.test", "root", "token", new StubHttpClientFactory(whm));

        var usage = await api.ListAccountsAsync(CancellationToken.None);

        usage.Should().BeEmpty();
    }

    /// <summary>The token form sends the whm scheme with user and token.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Create_SendsTheTokenInTheAuthorizationHeaderAsync()
    {
        whm.Enqueue("""{ "data": { "acct": [] } }""");
        var api = CpanelWhmApi.Create("whm.test", "root", "token", new StubHttpClientFactory(whm));

        await api.ListAccountsAsync(CancellationToken.None);

        var call = whm.Requests.Should().ContainSingle().Which;
        call.PathAndQuery.Should().Be("/json-api/listaccts?api.version=1");
        call.Authorization.Should().Be("whm root:token");
    }

    /// <summary>The hash form strips the line breaks the stored hash is wrapped with.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateWithHash_StripsWhitespaceFromTheHashAsync()
    {
        whm.Enqueue("""{ "data": { "acct": [] } }""");
        var api = CpanelWhmApi.CreateWithHash("whm.test", "root", "abc\r\ndef\n", new StubHttpClientFactory(whm));

        await api.ListAccountsAsync(CancellationToken.None);

        whm.Requests.Should().ContainSingle().Which.Authorization.Should().Be("WHM root:abcdef");
    }

    /// <summary>Both ways in take the named WHM client, never the factory's unnamed one.</summary>
    [Fact]
    public void Create_AsksForTheNamedWhmClient()
    {
        var factory = new StubHttpClientFactory(whm);

        CpanelWhmApi.Create("whm.test", "root", "token", factory);
        CpanelWhmApi.CreateWithHash("whm.test", "root", "hash", factory);

        factory.ClientNames.Should().Equal(HttpClientNames.CPanelWhm, HttpClientNames.CPanelWhm);
    }

    /// <summary>A failing status is raised, not parsed as an empty listing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListAccounts_Throws_WhenWhmAnswersWithAnErrorStatusAsync()
    {
        whm.Enqueue(System.Net.HttpStatusCode.Forbidden, "denied");
        var api = CpanelWhmApi.Create("whm.test", "root", "token", new StubHttpClientFactory(whm));

        var act = () => api.ListAccountsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
