using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>
/// The agent contract as HostPanel speaks it, against a recording transport. The agent's own
/// side of the contract is tested in <c>backend/agent/ssh-key-agent/tests</c>.
/// </summary>
public sealed class AgentSshKeyProviderTests
{
    /// <summary>The agent endpoint of server 7 every provider under test talks to.</summary>
    private static readonly SshAgentEndpoint Endpoint = new(
        7, "agent.example.net", 2200, "hpsshkeys", "PRIVATE", "SHA256:abc", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    /// <summary>The account every contract vector acts on.</summary>
    private static readonly SshAccount VectorAccount = new(7, "alice");

    /// <summary>
    /// Gets the rows of the <c>add</c> vectors the host can build. A request with an empty <c>from</c> list is left out:
    /// the host never sends the field for a key without sources, so only the agent's own tests run that vector.
    /// </summary>
    public static TheoryData<int> AddVectors => AgentContractVectors.Rows(
        "add", vector => vector.Request["from"] is not JsonArray { Count: 0 });

    /// <summary>Gets the rows of the <c>ping</c> vectors.</summary>
    public static TheoryData<int> PingVectors => AgentContractVectors.Rows("ping");

    /// <summary>Adding a key sends exactly one version 1 <c>add</c> request for the account and key id to the server's own endpoint.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Add_SendsVersionedRequestForTheAccountToItsOwnEndpointAsync()
    {
        var transport = new RecordingTransport("""{"v":1,"ok":true,"changed":true}""");
        var keyId = Guid.NewGuid();

        await new AgentSshKeyProvider(Endpoint, transport.SendAsync).AddKeyAsync(new SshAccount(7, "web"), keyId, "ssh-ed25519 AAAA", [], default);

        var (endpoint, request) = Assert.Single(transport.Sent);
        Assert.Same(Endpoint, endpoint);
        Assert.Equal(1, request["v"]!.GetValue<int>());
        Assert.Equal("add", request["op"]!.GetValue<string>());
        Assert.Equal("web", request["account"]!.GetValue<string>());
        Assert.Equal(keyId.ToString(), request["key_id"]!.GetValue<string>());
        Assert.Equal(5, request.Count);
    }

    /// <summary>A key with sources sends them as the <c>from</c> list, in order.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Add_WithSources_SendsThemAsTheFromListAsync()
    {
        var transport = new RecordingTransport("""{"v":1,"ok":true,"changed":true}""");

        await new AgentSshKeyProvider(Endpoint, transport.SendAsync).AddKeyAsync(
            new SshAccount(7, "web"), Guid.NewGuid(), "ssh-ed25519 AAAA", ["203.0.113.7", "198.51.100.0/24"], default);

        var (_, request) = Assert.Single(transport.Sent);
        Assert.Equal(new[] { "203.0.113.7", "198.51.100.0/24" }, request["from"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(6, request.Count);
    }

    /// <summary>The receipt carries the host key of the answer, and an agent that sends none gives a receipt without one.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Add_ReceiptCarriesTheHostKey_AndToleratesOldAgentsAsync()
    {
        var current = new AgentSshKeyProvider(Endpoint, new RecordingTransport("""{"v":1,"ok":true,"changed":true,"host_key":"ssh-ed25519 AAAAhost"}""").SendAsync);
        var old = new AgentSshKeyProvider(Endpoint, new RecordingTransport("""{"v":1,"ok":true,"changed":true}""").SendAsync);
        var account = new SshAccount(7, "web");

        Assert.Equal(new SshKeyReceipt("ssh-ed25519 AAAAhost"), await current.AddKeyAsync(account, Guid.NewGuid(), "ssh-ed25519 AAAA", [], default));
        Assert.Equal(new SshKeyReceipt(null), await old.AddKeyAsync(account, Guid.NewGuid(), "ssh-ed25519 AAAA", [], default));
    }

    /// <summary>Every <c>add</c> vector: the request the host builds equals it, and the answer is read as it says.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(AddVectors))]
    public async Task Add_MatchesTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("add")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());
        var provider = new AgentSshKeyProvider(Endpoint, transport.SendAsync);
        var sources = vector.Request["from"]?.AsArray().Select(node => node!.GetValue<string>()).ToList() ?? [];
        var keyId = Guid.Parse(vector.Request["key_id"]!.GetValue<string>());
        var publicKey = vector.Request["public_key"]!.GetValue<string>();

        if (vector.Response["ok"]!.GetValue<bool>())
        {
            var receipt = await provider.AddKeyAsync(VectorAccount, keyId, publicKey, sources, default);
            Assert.Equal(vector.Response["host_key"]?.GetValue<string>(), receipt.HostKey);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<SshProviderException>(() => provider.AddKeyAsync(VectorAccount, keyId, publicKey, sources, default));
            Assert.Equal(
                vector.Response["error"]!.GetValue<string>() switch
                {
                    "key_id_conflict" => SshProviderFailure.KeyIdConflict,
                    "duplicate_key" => SshProviderFailure.DuplicateKey,
                    "invalid_request" => SshProviderFailure.InvalidRequest,
                    var other => throw new InvalidOperationException($"The vector names an error this test does not map: {other}."),
                },
                ex.Failure);
        }

        Assert.True(JsonNode.DeepEquals(vector.Request, Assert.Single(transport.Sent).Request));
    }

    /// <summary>Every <c>ping</c> vector: the host key of the answer is read, and an answer without one gives none.</summary>
    /// <param name="index">The vector's position in its file.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(PingVectors))]
    public async Task Ping_ReadsTheHostKeyOfTheGoldenVectorAsync(int index)
    {
        var vector = AgentContractVectors.Load("ping")[index];
        var transport = new RecordingTransport(vector.Response.ToJsonString());

        var info = await new AgentSshKeyProvider(Endpoint, transport.SendAsync).PingAsync(default);

        Assert.True(JsonNode.DeepEquals(vector.Request, Assert.Single(transport.Sent).Request));
        Assert.Equal(vector.Response["host_key"]?.GetValue<string>(), info.HostKey);
    }

    /// <summary>Removing a jail sends one <c>remove-jail</c> request for the account and reads whether a jail was removed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RemoveJail_SendsAccountAndReadsRemovedAsync()
    {
        var transport = new RecordingTransport("""{"v":1,"ok":true,"removed":true}""");

        var removed = await new AgentSshKeyProvider(Endpoint, transport.SendAsync).RemoveDeletedAccountJailAsync(new SshAccount(7, "web"), default);

        Assert.True(removed);
        var (_, request) = Assert.Single(transport.Sent);
        Assert.Equal("remove-jail", request["op"]!.GetValue<string>());
        Assert.Equal("web", request["account"]!.GetValue<string>());
        Assert.Equal(3, request.Count);
    }

    /// <summary>An agent refusing because the account still exists maps to <see cref="SshProviderFailure.AccountExists"/>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RemoveJail_AccountExists_MapsToItsFailureAsync()
    {
        var provider = new AgentSshKeyProvider(Endpoint, new RecordingTransport("""{"v":1,"ok":false,"error":"account_exists"}""").SendAsync);

        var ex = await Assert.ThrowsAsync<SshProviderException>(() => provider.RemoveDeletedAccountJailAsync(new SshAccount(7, "web"), default));

        Assert.Equal(SshProviderFailure.AccountExists, ex.Failure);
    }

    /// <summary>Ping reads the version, profile and binding, and an agent older than bindings answers with neither.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Ping_ReadsVersionProviderAndBinding_AndToleratesOldAgentsAsync()
    {
        var binding = Guid.NewGuid();
        var current = new AgentSshKeyProvider(Endpoint, new RecordingTransport($$"""{"v":1,"ok":true,"agent_version":"1.1.0","provider":"cwp","binding":"{{binding}}"}""").SendAsync);
        var old = new AgentSshKeyProvider(Endpoint, new RecordingTransport("""{"v":1,"ok":true,"agent_version":"1.0.0"}""").SendAsync);

        Assert.Equal(new SshAgentInfo("1.1.0", "cwp", binding), await current.PingAsync(default));
        Assert.Equal(new SshAgentInfo("1.0.0", null, null), await old.PingAsync(default));
    }

    /// <summary>An account that belongs to another server is refused before any request reaches the transport.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AccountOfAnotherServer_RefusedBeforeAnythingIsSentAsync()
    {
        var transport = new RecordingTransport("""{"v":1,"ok":true}""");
        var provider = new AgentSshKeyProvider(Endpoint, transport.SendAsync);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RevokeKeyAsync(new SshAccount(8, "web"), Guid.NewGuid(), default));
        Assert.Empty(transport.Sent);
    }

    /// <summary>Listing keeps the managed keys with a valid key id and skips entries whose id does not parse.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task List_ParsesManagedKeysAsync()
    {
        var id = Guid.NewGuid();
        var transport = new RecordingTransport($$"""{"v":1,"ok":true,"keys":[{"key_id":"{{id}}","fingerprint":"SHA256:f"},{"key_id":"bogus"}],"unmanaged_keys":2}""");

        var keys = await new AgentSshKeyProvider(Endpoint, transport.SendAsync).ListKeysAsync(new SshAccount(7, "web"), default);

        Assert.Equal(new InstalledSshKey(id, "SHA256:f"), Assert.Single(keys));
    }

    /// <summary>Every agent refusal, unknown version or malformed answer becomes a provider exception with the matching failure and outcome-unknown flag.</summary>
    /// <param name="response">The raw agent answer.</param>
    /// <param name="failure">The failure the exception must carry.</param>
    /// <param name="unknown">Whether the outcome on the server must be reported as unknown.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("""{"v":1,"ok":false,"error":"duplicate_key"}""", SshProviderFailure.DuplicateKey, false)]
    [InlineData("""{"v":1,"ok":false,"error":"too_many_keys"}""", SshProviderFailure.TooManyKeys, false)]
    [InlineData("""{"v":1,"ok":false,"error":"ssh_disabled"}""", SshProviderFailure.SshDisabled, false)]
    [InlineData("""{"v":1,"ok":false,"error":"invalid_key"}""", SshProviderFailure.InvalidKey, false)]
    [InlineData("""{"v":1,"ok":false,"error":"account_suspended"}""", SshProviderFailure.AccountSuspended, false)]
    [InlineData("""{"v":1,"ok":false,"error":"something_new"}""", SshProviderFailure.Unknown, false)]
    [InlineData("""{"v":1,"ok":false}""", SshProviderFailure.InternalError, true)]
    [InlineData("""{"v":2,"ok":true}""", SshProviderFailure.UnsupportedVersion, false)]
    [InlineData("not json", SshProviderFailure.InternalError, true)]
    [InlineData("""{"v":1,"ok":false,"error":"timeout"}""", SshProviderFailure.Timeout, true)]
    public async Task Refusals_BecomeProviderExceptionsWithCodesAsync(string response, SshProviderFailure failure, bool unknown)
    {
        var provider = new AgentSshKeyProvider(Endpoint, new RecordingTransport(response).SendAsync);

        var ex = await Assert.ThrowsAsync<SshProviderException>(() => provider.GetAccessStatusAsync(new SshAccount(7, "web"), default));

        Assert.Equal(failure, ex.Failure);
        Assert.Equal(unknown, ex.IsOutcomeUnknown);
    }

    /// <summary>The factory builds an agent provider only for supported modules and refuses missing or incomplete settings.</summary>
    [Fact]
    public void Factory_OnlyTheAgentModuleSupported_AndSettingsMustBeComplete()
    {
        var factory = SshAgentTestSeeds.KeyFactory();

        Assert.IsType<AgentSshKeyProvider>(factory.CreateFor(Target(ServerModules.Cwp7, Agent(isComplete: true))));
        Assert.Null(factory.CreateFor(Target(ServerModules.CPanel, Agent(isComplete: true))));
        Assert.Throws<InvalidOperationException>(() => factory.CreateFor(Target(ServerModules.Cwp7, null)));
        Assert.Throws<InvalidOperationException>(() => factory.CreateFor(Target(ServerModules.Cwp7, Agent(isComplete: false))));
    }

    /// <summary>Builds the SSH target of server 7 for the given module.</summary>
    /// <param name="module">The control panel module of the server.</param>
    /// <param name="agent">The stored agent settings, or null when none are stored.</param>
    /// <returns>The SSH target.</returns>
    private static ServerSshTargetDto Target(string module, ServerSshAgentEndpoint? agent) =>
        new(7, module, false, agent);

    /// <summary>Builds agent settings for server 7.</summary>
    /// <param name="isComplete">Whether every value an agent call needs is present.</param>
    /// <returns>The agent settings.</returns>
    private static ServerSshAgentEndpoint Agent(bool isComplete) =>
        new(true, isComplete, "h", 22, "h", 22, "hp", "KEY", "SHA256:x", 5, 5);
}
