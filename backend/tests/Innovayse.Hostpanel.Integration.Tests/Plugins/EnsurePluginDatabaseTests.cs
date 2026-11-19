using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostHostingApi.EnsureDatabaseAsync"/> through <c>PluginHostingService</c>: the database created on the
/// service's own account with its password handed out once, the label rules, each gate refusal in its order with no agent
/// call, idempotency from the stored record (a repeat answers a null password), another plugin's label (Conflict), the
/// limits, the budget failing closed, concurrent calls, and the password in no column, log line or printed form.
/// </summary>
/// <param name="fixture">The host with the hosting plugins.</param>
public sealed class EnsurePluginDatabaseTests(PluginHostAccessFixture fixture) : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>A bound service of the caller gets the database on its own account, with the password once, and an audit line without it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BoundService_GetsTheDatabaseWithThePasswordOnceAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-ok", fixture.HostingPluginId);

        var database = await EnsureAsync(owner, fixture.HostingPluginId, " shop ");

        database.Name.Should().Be("web_shop");
        database.Username.Should().Be("web_shop");
        database.Host.Should().Be("127.0.0.1");
        database.Port.Should().Be(3306);
        database.Password.Should().Be(FakeHostingAgent.DatabasePassword);
        fixture.HostingAgents().AgentFor(owner.ServerId).HasDatabase("web", "shop").Should().BeTrue();
        var stored = (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        (stored.DatabaseId, stored.Label, stored.Name, stored.State, stored.IssuedToPluginId)
            .Should().Be((database.Id, "shop", "web_shop", HostingResourceState.Active, fixture.HostingPluginId));
        (await fixture.ActivityOfAsync(owner.ClientId)).Should()
            .Contain($"Plugin {fixture.HostingPluginId} set up database web_shop on service #{owner.ServiceId}")
            .And.NotContain(line => line.Contains(FakeHostingAgent.DatabasePassword, StringComparison.Ordinal));
    }

    /// <summary>A repeat for the same label answers the same database with a null password and does not call the server again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Repeat_AnswersFromTheRecordWithANullPasswordAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-repeat", fixture.HostingPluginId);
        var first = await EnsureAsync(owner, fixture.HostingPluginId, "shop");

        var second = await EnsureAsync(owner, fixture.HostingPluginId, "shop");

        second.Id.Should().Be(first.Id);
        second.Name.Should().Be(first.Name);
        second.Password.Should().BeNull();
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("ensure-database").Should().Be(1);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle();
    }

    /// <summary>Labels the agent would refuse are refused first, before any agent call and before a record is saved.</summary>
    /// <param name="label">The label.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1shop")]
    [InlineData("Shop")]
    [InlineData("my-shop")]
    [InlineData("abcdefghijklmnop")]
    public async Task InvalidLabels_AreRefusedBeforeAnyCallAsync(string label)
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-label", fixture.HostingPluginId);

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, label);

        failure.Should().Be(PluginHostAccessFailure.InvalidRequest);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>The gate's refusals in their order, each with no agent call and no record.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GateRefusals_AreAnsweredWithNoAgentCallAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-gate", fixture.HostingPluginId);
        var stranger = await fixture.OwnerWithServiceAsync("db-gate-stranger");
        var unbound = await fixture.OwnerWithDomainBoundToAsync("db-gate-unbound");

        var sshOnly = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId, "shop");
        var websitesOnly = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.WebsitesPluginId, owner.ClientId), owner.ServiceId, "shop");
        var plain = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.PlainPluginId, owner.ClientId), owner.ServiceId, "shop");
        var admin = await RefusalAsync(PluginCallBinding.WithoutClient(fixture.HostingPluginId), owner.ServiceId, "shop");
        var noRight = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(
                fixture.HostingPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords),
            owner.ServiceId,
            "shop");
        var foreign = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, stranger.ClientId), owner.ServiceId, "shop");
        var missing = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), int.MaxValue, "shop");
        var notBound = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, unbound.ClientId), unbound.ServiceId, "shop");

        new[] { sshOnly, websitesOnly, plain, admin, noRight, notBound }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotAllowed);
        new[] { foreign, missing }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotFound);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        fixture.HostingAgents().AgentFor(unbound.ServerId).Calls.Should().BeEmpty();
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A server with SSH switched off cannot take databases: Unavailable, with no agent call and no record.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServerWithoutAnAgent_IsUnavailableAsync()
    {
        var owner = await fixture.OwnerOnSshDisabledServerAsync("db-disabled");
        await fixture.BindProductsOfAsync(fixture.HostingPluginId, owner.ServiceId);

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, "shop");

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A label that is another plugin's is a Conflict and the server is not called a second time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherPluginsLabel_IsAConflictAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-conflict", fixture.HostingPluginId, fixture.OtherHostingPluginId);
        await EnsureAsync(owner, fixture.HostingPluginId, "shop");

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.OtherHostingPluginId, owner.ClientId), owner.ServiceId, "shop");

        failure.Should().Be(PluginHostAccessFailure.Conflict);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("ensure-database").Should().Be(1);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.IssuedToPluginId.Should().Be(fixture.HostingPluginId);
    }

    /// <summary>The server's own refusals map to the SDK's reasons, and a definite refusal leaves no record behind.</summary>
    /// <param name="agentFailure">What the agent answers.</param>
    /// <param name="expected">What the plugin is told.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(SshProviderFailure.DatabaseLimit, PluginHostAccessFailure.LimitReached)]
    [InlineData(SshProviderFailure.SiteNotOwned, PluginHostAccessFailure.Conflict)]
    [InlineData(SshProviderFailure.NotSupported, PluginHostAccessFailure.Unavailable)]
    [InlineData(SshProviderFailure.DatabaseFailed, PluginHostAccessFailure.ServerUnavailable)]
    public async Task DefiniteAgentRefusal_IsMappedAndLeavesNoRecordAsync(SshProviderFailure agentFailure, PluginHostAccessFailure expected)
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync($"db-refused-{agentFailure}".ToLowerInvariant(), fixture.HostingPluginId);
        fixture.HostingAgents().AgentFor(owner.ServerId).FailWith = agentFailure;

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, "shop");

        failure.Should().Be(expected);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A server that does not answer leaves a pending record, and the retry resumes it without losing the label.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownOutcome_KeepsAPendingRecordThatARetryResumesAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-unknown", fixture.HostingPluginId);
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);
        agent.FailWith = SshProviderFailure.Timeout;

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, "shop");
        var pending = (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        agent.FailWith = null;
        var database = await EnsureAsync(owner, fixture.HostingPluginId, "shop");

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        pending.State.Should().Be(HostingResourceState.Pending);
        database.Id.Should().Be(pending.DatabaseId);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>A service already holding as many databases as the host allows is refused before the server is called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceAtTheDatabaseLimit_IsLimitReachedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-limit", fixture.HostingPluginId);
        await fixture.SeedDatabasesAsync(owner, fixture.HostingPluginId, HostingResourceLimits.MaxDatabasesPerService);

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, "shop");

        failure.Should().Be(PluginHostAccessFailure.LimitReached);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().HaveCount(HostingResourceLimits.MaxDatabasesPerService);
    }

    /// <summary>Parallel adds of different labels on a service one short of the limit let exactly one through.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ParallelAddsOneBelowTheLimit_LetExactlyOneThroughAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-race", fixture.HostingPluginId);
        await fixture.SeedDatabasesAsync(owner, fixture.HostingPluginId, HostingResourceLimits.MaxDatabasesPerService - 1);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => TryEnsureAsync(binding, owner.ServiceId, $"race{i}")));

        outcomes.Count(o => o is null).Should().Be(1);
        outcomes.Where(o => o is not null).Should().OnlyContain(o => o == PluginHostAccessFailure.LimitReached || o == PluginHostAccessFailure.ServerUnavailable);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().HaveCount(HostingResourceLimits.MaxDatabasesPerService);
    }

    /// <summary>Parallel calls for the same label end as one database, with the one id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ParallelCallsForOneLabel_EndAsOneDatabaseAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-same", fixture.HostingPluginId);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);

        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            fixture.AsHostingPluginAsync(binding, api => api.EnsureDatabaseAsync(owner.ServiceId, "shop", CancellationToken.None))
                .ContinueWith(t => t.IsCompletedSuccessfully ? t.Result.Id : Guid.Empty, TaskScheduler.Default)));

        var stored = (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        answers.Where(id => id != Guid.Empty).Should().NotBeEmpty().And.OnlyContain(id => id == stored.DatabaseId);
        stored.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>A spent budget refuses before the server is called, and a limiter that cannot answer refuses too (fail closed).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SpentBudgetAndDeadLimiter_RefuseBeforeTheServerIsCalledAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-budget", fixture.HostingPluginId);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);
        using var budgeted = fixture.HostWithHostingBudget(1);
        await PluginHostAccessFixture.AsPluginAsync(
            budgeted, binding, api => ((IPluginHostHostingApi)api).EnsureDatabaseAsync(owner.ServiceId, "one", CancellationToken.None));

        var spent = await PluginHostAccessFixture.AsPluginAsync(budgeted, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).EnsureDatabaseAsync(owner.ServiceId, "two", CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
        using var dead = fixture.HostWithADeadLimiter();
        await PluginHostAccessFixture.AsPluginAsync(dead, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).EnsureDatabaseAsync(owner.ServiceId, "three", CancellationToken.None);
            await act.Should().ThrowAsync<ObjectDisposedException>();
            return true;
        });

        spent.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        PluginHostAccessFixture.HostingAgentsOf(budgeted).AgentFor(owner.ServerId).CallsOf("ensure-database").Should().Be(1);
        PluginHostAccessFixture.HostingAgentsOf(dead).AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle();
    }

    /// <summary>A plugin switched off while its database is being created gets nothing: NotAllowed, and the database is dropped and recorded as removed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginSwitchedOffDuringTheCall_GetsNothingAndTheDatabaseIsDroppedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-off-during", fixture.DatabasesPluginId);
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);
        agent.WhileEnsuring = () => fixture.SwitchAsync(fixture.DatabasesPluginId, enabled: false);
        try
        {
            var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.DatabasesPluginId, owner.ClientId), owner.ServiceId, "shop");

            failure.Should().Be(PluginHostAccessFailure.NotAllowed);
            agent.HasDatabase("web", "shop").Should().BeFalse();
            (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Removed);
        }
        finally
        {
            agent.WhileEnsuring = null;
            await fixture.SwitchAsync(fixture.DatabasesPluginId, enabled: true);
        }
    }

    /// <summary>The password is in no text, JSON or byte column of any table, in no log line, and not in the printed form of what the host handed out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Password_IsNeverStoredOrLoggedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("db-secret", fixture.HostingPluginId);

        var database = await EnsureAsync(owner, fixture.HostingPluginId, "shop");
        await EnsureAsync(owner, fixture.HostingPluginId, "shop");

        database.Password.Should().Be(FakeHostingAgent.DatabasePassword);
        database.ToString().Should().NotContain(FakeHostingAgent.DatabasePassword);
        (await fixture.DatabaseHoldsAsync(FakeHostingAgent.DatabasePassword)).Should().BeFalse("the password must not be persisted anywhere");
        fixture.LoggedMessages.Should().NotContain(message => message.Contains(FakeHostingAgent.DatabasePassword, StringComparison.Ordinal));
    }

    /// <summary>Creates a database as a plugin does and returns the answer.</summary>
    /// <param name="owner">The service's owner scenario.</param>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="label">The label.</param>
    /// <returns>The database.</returns>
    private Task<PluginDatabase> EnsureAsync(SshOwnerScenario owner, string pluginId, string label)
    {
        return fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(pluginId, owner.ClientId),
            api => api.EnsureDatabaseAsync(owner.ServiceId, label, CancellationToken.None));
    }

    /// <summary>Creates a database and returns the refusal the plugin catches.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="label">The label.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(PluginCallBinding binding, int serviceId, string label)
    {
        return await fixture.AsHostingPluginAsync(binding, async api =>
        {
            var act = () => api.EnsureDatabaseAsync(serviceId, label, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
    }

    /// <summary>Creates a database and answers the refusal, or <see langword="null"/> when it was created.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="label">The label.</param>
    /// <returns>The failure the host reported, if any.</returns>
    private async Task<PluginHostAccessFailure?> TryEnsureAsync(PluginCallBinding binding, int serviceId, string label)
    {
        try
        {
            await fixture.AsHostingPluginAsync(binding, api => api.EnsureDatabaseAsync(serviceId, label, CancellationToken.None));
            return null;
        }
        catch (PluginHostAccessException ex)
        {
            return ex.Failure;
        }
    }
}
