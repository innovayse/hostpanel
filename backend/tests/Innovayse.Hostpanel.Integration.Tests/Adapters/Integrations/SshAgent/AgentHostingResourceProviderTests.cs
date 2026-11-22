using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>
/// The website and database operations of the agent contract as HostPanel speaks them, against a recording transport and
/// the agent's own golden vectors (<c>backend/agent/ssh-key-agent/tests/contract</c>, linked into the output): every
/// request the host builds equals the vector's request, and every answer, success or refusal, is read as the vector says.
/// </summary>
public sealed class AgentHostingResourceProviderTests
{
    /// <summary>The agent endpoint of server 7 every provider under test talks to.</summary>
    private static readonly SshAgentEndpoint Endpoint = new(
        7, "agent.example.net", 2200, "hpsshkeys", "PRIVATE", "SHA256:abc", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    /// <summary>The account every vector acts on.</summary>
    private static readonly SshAccount Account = new(7, "alice");

    /// <summary>The request fields <c>ensure-website</c> may carry; a vector with another field stands for a request the host never builds.</summary>
    private static readonly string[] WebsiteFields = ["v", "op", "account", "hostname", "docroot", "php", "link_root"];

    /// <summary>The request fields <c>remove-website</c> carries.</summary>
    private static readonly string[] RemoveWebsiteFields = ["v", "op", "account", "hostname"];

    /// <summary>The request fields the database operations carry.</summary>
    private static readonly string[] DatabaseFields = ["v", "op", "account", "label"];

    /// <summary>Gets the rows of the <c>ensure-website</c> vectors.</summary>
    public static TheoryData<int> EnsureWebsiteVectors => AgentContractVectors.Rows("ensure-website");

    /// <summary>Gets the rows of the <c>remove-website</c> vectors.</summary>
    public static TheoryData<int> RemoveWebsiteVectors => AgentContractVectors.Rows("remove-website");

    /// <summary>Gets the rows of the <c>ensure-database</c> vectors.</summary>
    public static TheoryData<int> EnsureDatabaseVectors => AgentContractVectors.Rows("ensure-database");

    /// <summary>Gets the rows of the <c>remove-database</c> vectors.</summary>
    public static TheoryData<int> RemoveDatabaseVectors => AgentContractVectors.Rows("remove-database");

    /// <summary>Gets the rows of the <c>ping</c> vectors.</summary>
    public static TheoryData<int> PingVectors => AgentContractVectors.Rows("ping");

    /// <summary>Every <c>ensure-website</c> vector: the request the host builds equals it and the answer is read as it says.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(EnsureWebsiteVectors))]
    public async Task EnsureWebsite_MatchesTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("ensure-website")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());
        var provider = new AgentHostingResourceProvider(Endpoint, transport.SendAsync);
        var order = new WebsiteOrder(
            Account,
            Guid.NewGuid(),
            TextOf(vector.Request, "hostname"),
            TextOf(vector.Request, "docroot"),
            vector.Request["php"]?.GetValue<string>(),
            vector.Request["link_root"]?.GetValue<bool>() == true);

        await ExpectAsync(vector, transport, WebsiteFields, () => provider.EnsureWebsiteAsync(order, default));
    }

    /// <summary>Every <c>remove-website</c> vector.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(RemoveWebsiteVectors))]
    public async Task RemoveWebsite_MatchesTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("remove-website")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());
        var provider = new AgentHostingResourceProvider(Endpoint, transport.SendAsync);
        var removal = new WebsiteRemoval(Account, Guid.NewGuid(), TextOf(vector.Request, "hostname"));

        await ExpectAsync(vector, transport, RemoveWebsiteFields, () => provider.RemoveWebsiteAsync(removal, default));
    }

    /// <summary>Every <c>ensure-database</c> vector, with the password carried only by the answer to the call that created the database.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(EnsureDatabaseVectors))]
    public async Task EnsureDatabase_MatchesTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("ensure-database")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());
        var provider = new AgentHostingResourceProvider(Endpoint, transport.SendAsync);
        var order = new DatabaseOrder(Account, Guid.NewGuid(), TextOf(vector.Request, "label"));
        ProvisionedDatabase? database = null;

        await ExpectAsync(vector, transport, DatabaseFields, async () => database = await provider.EnsureDatabaseAsync(order, default));

        if (vector.Response["ok"]!.GetValue<bool>())
        {
            Assert.Equal(vector.Response["database"]!.GetValue<string>(), database!.Name);
            Assert.Equal(vector.Response["username"]!.GetValue<string>(), database.Username);
            Assert.Equal(vector.Response["password"]?.GetValue<string>(), database.Password);
        }
    }

    /// <summary>Every <c>remove-database</c> vector.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(RemoveDatabaseVectors))]
    public async Task RemoveDatabase_MatchesTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("remove-database")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());
        var provider = new AgentHostingResourceProvider(Endpoint, transport.SendAsync);
        var removal = new DatabaseRemoval(Account, Guid.NewGuid(), TextOf(vector.Request, "label"), "alice_shop", "alice_shop");

        await ExpectAsync(vector, transport, DatabaseFields, () => provider.RemoveDatabaseAsync(removal, default));
    }

    /// <summary>Every <c>ping</c> vector: a website or database capability is reported only when the agent lists both of its operations.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(PingVectors))]
    public async Task Ping_MatchesTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("ping")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());
        var provider = new AgentHostingResourceProvider(Endpoint, transport.SendAsync);
        var listed = vector.Response["capabilities"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();

        var capabilities = await provider.GetCapabilitiesAsync(default);

        Assert.True(JsonNode.DeepEquals(vector.Request, Assert.Single(transport.Sent).Request));
        Assert.Equal(listed.Contains("ensure-website") && listed.Contains("remove-website"), capabilities.Websites);
        Assert.Equal(listed.Contains("ensure-database") && listed.Contains("remove-database"), capabilities.Databases);
        Assert.Equal(vector.Response["php_versions"]!.AsArray().Select(node => node!.GetValue<string>()), capabilities.PhpVersions);
    }

    /// <summary>The PHP versions of the answer are reported in the agent's order; an agent that does not send them reports none.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Ping_ReportsThePhpVersionsTheAgentSendsAsync()
    {
        var withVersions = new RecordingTransport(
            """{"v":1,"ok":true,"agent_version":"1.5.0","capabilities":["ensure-website","remove-website"],"php_versions":["8.1","8.3"]}""");
        var without = new RecordingTransport("""{"v":1,"ok":true,"agent_version":"1.4.0"}""");

        var newer = await new AgentHostingResourceProvider(Endpoint, withVersions.SendAsync).GetCapabilitiesAsync(default);
        var older = await new AgentHostingResourceProvider(Endpoint, without.SendAsync).GetCapabilitiesAsync(default);

        Assert.True(newer.Websites);
        Assert.False(newer.Databases);
        Assert.Equal(["8.1", "8.3"], newer.PhpVersions);
        Assert.False(older.Websites);
        Assert.False(older.Databases);
        Assert.Empty(older.PhpVersions);
    }

    /// <summary>
    /// A hosting operation is sent over the server's endpoint with a timeout that outlasts the agent's own helper limit, and
    /// a longer one the administrator configured is kept.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task HostingOperations_GetATimeoutThatOutlastsTheHelperAsync()
    {
        var transport = new RecordingTransport("""{"v":1,"ok":true,"changed":true}""");
        var longer = Endpoint with { OperationTimeout = TimeSpan.FromMinutes(5) };

        await new AgentHostingResourceProvider(Endpoint, transport.SendAsync)
            .RemoveWebsiteAsync(new WebsiteRemoval(Account, Guid.NewGuid(), "app.example.com"), default);
        await new AgentHostingResourceProvider(longer, transport.SendAsync)
            .RemoveWebsiteAsync(new WebsiteRemoval(Account, Guid.NewGuid(), "app.example.com"), default);

        Assert.True(transport.Sent[0].Endpoint.OperationTimeout >= TimeSpan.FromSeconds(60));
        Assert.Equal(TimeSpan.FromMinutes(5), transport.Sent[1].Endpoint.OperationTimeout);
        Assert.Equal(Endpoint.ServerId, transport.Sent[0].Endpoint.ServerId);
    }

    /// <summary>An account of another server is refused before anything is sent.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AccountOfAnotherServer_IsRefusedBeforeAnythingIsSentAsync()
    {
        var transport = new RecordingTransport("""{"v":1,"ok":true,"changed":true}""");
        var provider = new AgentHostingResourceProvider(Endpoint, transport.SendAsync);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnsureWebsiteAsync(new WebsiteOrder(new SshAccount(8, "alice"), Guid.NewGuid(), "a.example.com", "public_html", null, false), default));

        Assert.Empty(transport.Sent);
    }

    /// <summary>A database answer without names is an internal error of the agent, never a database with empty names.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DatabaseAnswerWithoutNames_IsAnInternalErrorAsync()
    {
        var provider = new AgentHostingResourceProvider(
            Endpoint, new RecordingTransport("""{"v":1,"ok":true,"created":true,"password":"0123456789abcdef0123456789abcdef01234567"}""").SendAsync);

        var ex = await Assert.ThrowsAsync<SshProviderException>(
            () => provider.EnsureDatabaseAsync(new DatabaseOrder(Account, Guid.NewGuid(), "shop"), default));

        Assert.Equal(SshProviderFailure.InternalError, ex.Failure);
        Assert.DoesNotContain("0123456789abcdef", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The refusal codes the hosting operations add read as their own failures, and a definite refusal is not an unknown outcome.</summary>
    /// <param name="code">The agent error code.</param>
    /// <param name="failure">The expected failure.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("website_conflict", SshProviderFailure.WebsiteConflict)]
    [InlineData("php_version_unsupported", SshProviderFailure.PhpVersionUnsupported)]
    [InlineData("website_failed", SshProviderFailure.WebsiteFailed)]
    [InlineData("database_failed", SshProviderFailure.DatabaseFailed)]
    [InlineData("database_limit", SshProviderFailure.DatabaseLimit)]
    [InlineData("site_not_owned", SshProviderFailure.SiteNotOwned)]
    [InlineData("not_supported", SshProviderFailure.NotSupported)]
    public async Task HostingRefusals_MapToTheirFailuresAsync(string code, SshProviderFailure failure)
    {
        var provider = new AgentHostingResourceProvider(Endpoint, new RecordingTransport($$"""{"v":1,"ok":false,"error":"{{code}}"}""").SendAsync);

        var ex = await Assert.ThrowsAsync<SshProviderException>(
            () => provider.EnsureWebsiteAsync(new WebsiteOrder(Account, Guid.NewGuid(), "app.example.com", "public_html", null, false), default));

        Assert.Equal(failure, ex.Failure);
        Assert.False(ex.IsOutcomeUnknown);
    }

    /// <summary>The factory builds a hosting provider for the modules the key factory supports and for no other, and refuses incomplete settings.</summary>
    [Fact]
    public void Factory_FollowsTheKeyFactorysSupport_AndSettingsMustBeComplete()
    {
        var transport = new SshAgentTransport(
            new OutboundConnector(Options.Create(new OutboundNetworkOptions()), Options.Create(new PanelNetworkOptions())),
            NullLogger<SshAgentTransport>.Instance);
        var factory = new HostingResourceProviderFactory(new SshKeyProviderFactory(transport), transport);

        Assert.IsType<AgentHostingResourceProvider>(factory.CreateFor(Target(ServerModules.Cwp7, Agent(isComplete: true))));
        Assert.Null(factory.CreateFor(Target(ServerModules.CPanel, Agent(isComplete: true))));
        Assert.Throws<InvalidOperationException>(() => factory.CreateFor(Target(ServerModules.Cwp7, null)));
        Assert.Throws<InvalidOperationException>(() => factory.CreateFor(Target(ServerModules.Cwp7, Agent(isComplete: false))));
    }

    /// <summary>
    /// Runs one vector: an answer that is ok must not throw, a refusal must throw the failure the code means; and when the
    /// vector is a request the host can build, the one it sent equals the vector's.
    /// </summary>
    /// <param name="vector">The golden vector.</param>
    /// <param name="transport">The transport that answered the vector's response.</param>
    /// <param name="buildableFields">The fields a request the host builds may have.</param>
    /// <param name="act">The provider call that answers the vector's request.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task ExpectAsync(AgentContractCase vector, RecordingTransport transport, string[] buildableFields, Func<Task> act)
    {
        if (vector.Response["ok"]!.GetValue<bool>())
        {
            await act();
        }
        else
        {
            var ex = await Assert.ThrowsAsync<SshProviderException>(act);
            Assert.Equal(AgentFailureCodes.Parse(vector.Response["error"]!.GetValue<string>()), ex.Failure);
        }

        // A vector with a field the host has no way to set (an unknown one) stands for a request only a broken client
        // sends; its answer is still read as above, but there is no request of ours to compare.
        if (vector.Request.All(field => buildableFields.Contains(field.Key)))
        {
            Assert.True(
                JsonNode.DeepEquals(vector.Request, Assert.Single(transport.Sent).Request),
                $"The request for \"{vector.Name}\" differs from the golden vector.");
        }
    }

    /// <summary>A required text field of a vector's request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="field">The field's name.</param>
    /// <returns>The text; a vector without the field cannot be built, so it is a test fault.</returns>
    private static string TextOf(JsonObject request, string field)
    {
        return request[field]!.GetValue<string>();
    }

    /// <summary>Builds the SSH target of server 7 for the given module.</summary>
    /// <param name="module">The control panel module of the server.</param>
    /// <param name="agent">The stored agent settings, or null when none are stored.</param>
    /// <returns>The SSH target.</returns>
    private static ServerSshTargetDto Target(string module, ServerSshAgentEndpoint? agent)
    {
        return new ServerSshTargetDto(7, module, false, agent);
    }

    /// <summary>Builds agent settings for server 7.</summary>
    /// <param name="isComplete">Whether every value an agent call needs is present.</param>
    /// <returns>The agent settings.</returns>
    private static ServerSshAgentEndpoint Agent(bool isComplete)
    {
        return new ServerSshAgentEndpoint(true, isComplete, "h", 22, "h", 22, "hp", "KEY", "SHA256:x", 5, 5);
    }
}
