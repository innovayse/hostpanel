using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostHostingApi.EnsureWebsiteAsync"/> through <c>PluginHostingService</c>: the website set up only on
/// the service's own account, the shape rules, each gate refusal in its order with no agent call, idempotency from the
/// stored record, a changed specification, another plugin's host name (Conflict), the per-service limit, the budget
/// failing closed, a plugin switched off mid-call, a server that refuses or does not answer, and concurrent calls.
/// </summary>
/// <param name="fixture">The host with the hosting plugins.</param>
public sealed class EnsurePluginWebsiteTests(PluginHostAccessFixture fixture) : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>A bound service of the caller gets the website on its own account, recorded as issued to the plugin and audited.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BoundService_GetsTheWebsiteOnItsOwnAccountAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-ok", fixture.HostingPluginId);

        var website = await EnsureAsync(owner, fixture.HostingPluginId, Spec("App.site-ok.example.test"));

        website.Hostname.Should().Be("app.site-ok.example.test");
        website.DocumentRoot.Should().Be("sites/app");
        website.DocumentRootPath.Should().Be("/home/web/sites/app");
        website.PhpVersion.Should().Be("8.3");
        var order = fixture.HostingAgents().AgentFor(owner.ServerId).WebsiteOf("web", "app.site-ok.example.test");
        order.Should().NotBeNull();
        order!.DocumentRootMayBeLink.Should().BeTrue();
        order.WebsiteId.Should().Be(website.Id);
        var stored = (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        stored.IssuedToPluginId.Should().Be(fixture.HostingPluginId);
        stored.State.Should().Be(HostingResourceState.Active);
        stored.IsBoundTo(owner.ServerId, "web").Should().BeTrue();
        (await fixture.ActivityOfAsync(owner.ClientId)).Should()
            .ContainSingle(line => line.StartsWith($"Plugin {fixture.HostingPluginId} set up website app.site-ok.example.test", StringComparison.Ordinal));
    }

    /// <summary>The service's own domain is allowed as a host name, and so is a deeper subdomain.</summary>
    /// <param name="prefix">What goes in front of the service's domain, with its dot.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("a.b.")]
    public async Task DomainAndSubdomains_AreAllowedAsync(string prefix)
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync($"site-names-{prefix.Length}", fixture.HostingPluginId);

        var website = await EnsureAsync(owner, fixture.HostingPluginId, Spec($"{prefix}site-names-{prefix.Length}.example.test"));

        website.Hostname.Should().Be($"{prefix}site-names-{prefix.Length}.example.test");
    }

    /// <summary>A website that is already as asked answers from the stored record: same id, no second call to the server.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RepeatWithTheSameSpecification_AnswersFromTheRecordAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-repeat", fixture.HostingPluginId);
        var first = await EnsureAsync(owner, fixture.HostingPluginId, Spec("app.site-repeat.example.test"));

        var second = await EnsureAsync(owner, fixture.HostingPluginId, Spec("app.site-repeat.example.test"));

        second.Should().Be(first);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("ensure-website").Should().Be(1);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle();
    }

    /// <summary>A repeat with another specification updates the server and the record and keeps the website's id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RepeatWithAChangedSpecification_UpdatesTheWebsiteAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-change", fixture.HostingPluginId);
        var first = await EnsureAsync(owner, fixture.HostingPluginId, Spec("app.site-change.example.test"));

        var second = await EnsureAsync(
            owner, fixture.HostingPluginId, new PluginWebsiteSpec("app.site-change.example.test", "public_html", "8.2", false));

        second.Id.Should().Be(first.Id);
        second.DocumentRoot.Should().Be("public_html");
        second.PhpVersion.Should().Be("8.2");
        var order = fixture.HostingAgents().AgentFor(owner.ServerId).WebsiteOf("web", "app.site-change.example.test")!;
        (order.DocumentRoot, order.PhpVersion, order.DocumentRootMayBeLink).Should().Be(("public_html", "8.2", false));
        var stored = (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        (stored.DocumentRoot, stored.PhpVersion, stored.DocumentRootMayBeLink).Should().Be(("public_html", "8.2", false));
    }

    /// <summary>A request the agent would refuse is refused first, before any agent call and before a record is saved.</summary>
    /// <param name="hostname">The host name asked for, relative to the service's domain when it starts with a dot.</param>
    /// <param name="documentRoot">The document root.</param>
    /// <param name="php">The PHP version.</param>
    /// <param name="mayBeLink">Whether the root may be a link.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("other.com", "public_html", "8.3", false)]
    [InlineData("evil-site-shape.example.test.evil.com", "public_html", "8.3", false)]
    [InlineData("not a host.site-shape.example.test", "public_html", "8.3", false)]
    [InlineData(".", "../bob", "8.3", false)]
    [InlineData(".", "/etc", "8.3", false)]
    [InlineData(".", ".ssh", "8.3", false)]
    [InlineData(".", "a/b/c/d/e/f/g", "8.3", false)]
    [InlineData(".", "public_html", "8", false)]
    [InlineData(".", "public_html", "php8.3", false)]
    [InlineData(".", "public_html", null, true)]
    public async Task InvalidShapes_AreRefusedBeforeAnyCallAsync(string hostname, string documentRoot, string? php, bool mayBeLink)
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-shape", fixture.HostingPluginId);
        var name = hostname == "." ? "app.site-shape.example.test" : hostname;

        var failure = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId,
            new PluginWebsiteSpec(name, documentRoot, php, mayBeLink));

        failure.Should().Be(PluginHostAccessFailure.InvalidRequest);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>The gate's refusals in their order, each with no agent call and no record.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GateRefusals_AreAnsweredWithNoAgentCallAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-gate", fixture.HostingPluginId);
        var stranger = await fixture.OwnerWithServiceAsync("site-gate-stranger");
        var unbound = await fixture.OwnerWithDomainBoundToAsync("site-gate-unbound");
        var spec = Spec("app.site-gate.example.test");

        var sshOnly = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId, spec);
        var databasesOnly = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.DatabasesPluginId, owner.ClientId), owner.ServiceId, spec);
        var plain = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.PlainPluginId, owner.ClientId), owner.ServiceId, spec);
        var admin = await RefusalAsync(PluginCallBinding.WithoutClient(fixture.HostingPluginId), owner.ServiceId, spec);
        var noRight = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(
                fixture.HostingPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords),
            owner.ServiceId,
            spec);
        var foreign = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, stranger.ClientId), owner.ServiceId, spec);
        var missing = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), int.MaxValue, spec);
        var notBound = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, unbound.ClientId), unbound.ServiceId, Spec("app.site-gate-unbound.example.test"));

        new[] { sshOnly, databasesOnly, plain, admin, noRight, notBound }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotAllowed);
        new[] { foreign, missing }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotFound);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        fixture.HostingAgents().AgentFor(unbound.ServerId).Calls.Should().BeEmpty();
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A product listed only in the plugin's own manifest is not bound, however many ids it claims.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProductBoundToAnotherPluginOnly_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-other-bound", fixture.OtherHostingPluginId);

        var failure = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, Spec("app.site-other-bound.example.test"));

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>A server with SSH switched off cannot take sites: Unavailable, with no agent call and no record.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServerWithoutAnAgent_IsUnavailableAsync()
    {
        var owner = await fixture.OwnerOnSshDisabledServerAsync("site-disabled");
        await fixture.SetDomainAsync(owner.ServiceId, "site-disabled.example.test");
        await fixture.BindProductsOfAsync(fixture.HostingPluginId, owner.ServiceId);

        var failure = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, Spec("app.site-disabled.example.test"));

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A host name that is another plugin's is a Conflict and the server is not called a second time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherPluginsHostname_IsAConflictAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-conflict", fixture.HostingPluginId, fixture.OtherHostingPluginId);
        await EnsureAsync(owner, fixture.HostingPluginId, Spec("app.site-conflict.example.test"));

        var failure = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.OtherHostingPluginId, owner.ClientId), owner.ServiceId, Spec("app.site-conflict.example.test"));

        failure.Should().Be(PluginHostAccessFailure.Conflict);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("ensure-website").Should().Be(1);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.IssuedToPluginId.Should().Be(fixture.HostingPluginId);
    }

    /// <summary>The server's own refusals map to the SDK's reasons, and a definite refusal leaves no record behind.</summary>
    /// <param name="agentFailure">What the agent answers.</param>
    /// <param name="expected">What the plugin is told.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(SshProviderFailure.WebsiteConflict, PluginHostAccessFailure.Conflict)]
    [InlineData(SshProviderFailure.SiteNotOwned, PluginHostAccessFailure.Conflict)]
    [InlineData(SshProviderFailure.PhpVersionUnsupported, PluginHostAccessFailure.InvalidRequest)]
    [InlineData(SshProviderFailure.NotSupported, PluginHostAccessFailure.Unavailable)]
    [InlineData(SshProviderFailure.AccountSuspended, PluginHostAccessFailure.Unavailable)]
    [InlineData(SshProviderFailure.WebsiteFailed, PluginHostAccessFailure.ServerUnavailable)]
    public async Task DefiniteAgentRefusal_IsMappedAndLeavesNoRecordAsync(SshProviderFailure agentFailure, PluginHostAccessFailure expected)
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync($"site-refused-{agentFailure}".ToLowerInvariant(), fixture.HostingPluginId);
        fixture.HostingAgents().AgentFor(owner.ServerId).FailWith = agentFailure;

        var failure = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId),
            owner.ServiceId,
            Spec($"app.site-refused-{agentFailure}.example.test".ToLowerInvariant()));

        failure.Should().Be(expected);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A server that does not answer leaves a pending record, so a retry resumes it with the same id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownOutcome_KeepsAPendingRecordThatARetryResumesAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-unknown", fixture.HostingPluginId);
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);
        agent.FailWith = SshProviderFailure.Timeout;
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);

        var failure = await RefusalAsync(binding, owner.ServiceId, Spec("app.site-unknown.example.test"));
        var pending = (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        agent.FailWith = null;
        var website = await EnsureAsync(owner, fixture.HostingPluginId, Spec("app.site-unknown.example.test"));

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        pending.State.Should().Be(HostingResourceState.Pending);
        website.Id.Should().Be(pending.WebsiteId);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>A service already holding as many websites as the host allows is refused before the server is called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceAtTheWebsiteLimit_IsLimitReachedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-limit", fixture.HostingPluginId);
        await fixture.SeedWebsitesAsync(owner, fixture.HostingPluginId, HostingResourceLimits.MaxWebsitesPerService);

        var failure = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId, Spec("app.site-limit.example.test"));

        failure.Should().Be(PluginHostAccessFailure.LimitReached);
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().HaveCount(HostingResourceLimits.MaxWebsitesPerService);
    }

    /// <summary>
    /// Parallel adds of different host names on a service one short of the limit let exactly one through: the count and the
    /// insert are serialised by the service row, so the limit holds.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ParallelAddsOneBelowTheLimit_LetExactlyOneThroughAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-race", fixture.HostingPluginId);
        await fixture.SeedWebsitesAsync(owner, fixture.HostingPluginId, HostingResourceLimits.MaxWebsitesPerService - 1);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => TryEnsureAsync(binding, owner.ServiceId, Spec($"p{i}.site-race.example.test"))));

        outcomes.Count(o => o is null).Should().Be(1);
        outcomes.Where(o => o is not null).Should().OnlyContain(o => o == PluginHostAccessFailure.LimitReached || o == PluginHostAccessFailure.ServerUnavailable);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().HaveCount(HostingResourceLimits.MaxWebsitesPerService);
    }

    /// <summary>Parallel calls for the same host name end as one website, answered with one id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ParallelCallsForOneHostname_EndAsOneWebsiteAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-same", fixture.HostingPluginId);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);

        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            fixture.AsHostingPluginAsync(binding, api => api.EnsureWebsiteAsync(owner.ServiceId, Spec("app.site-same.example.test"), CancellationToken.None))
                .ContinueWith(t => t.IsCompletedSuccessfully ? t.Result.Id : Guid.Empty, TaskScheduler.Default)));

        var stored = (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which;
        answers.Where(id => id != Guid.Empty).Should().NotBeEmpty().And.OnlyContain(id => id == stored.WebsiteId);
        stored.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>A spent budget refuses before the server is called, and a limiter that cannot answer refuses too (fail closed).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SpentBudgetAndDeadLimiter_RefuseBeforeTheServerIsCalledAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-budget", fixture.HostingPluginId);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);
        using var budgeted = fixture.HostWithHostingBudget(1);
        await PluginHostAccessFixture.AsPluginAsync(
            budgeted, binding, api => ((IPluginHostHostingApi)api).EnsureWebsiteAsync(owner.ServiceId, Spec("one.site-budget.example.test"), CancellationToken.None));

        var spent = await PluginHostAccessFixture.AsPluginAsync(budgeted, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).EnsureWebsiteAsync(owner.ServiceId, Spec("two.site-budget.example.test"), CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
        using var dead = fixture.HostWithADeadLimiter();
        await PluginHostAccessFixture.AsPluginAsync(dead, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).EnsureWebsiteAsync(owner.ServiceId, Spec("three.site-budget.example.test"), CancellationToken.None);
            await act.Should().ThrowAsync<ObjectDisposedException>();
            return true;
        });

        spent.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        PluginHostAccessFixture.HostingAgentsOf(budgeted).AgentFor(owner.ServerId).CallsOf("ensure-website").Should().Be(1);
        PluginHostAccessFixture.HostingAgentsOf(dead).AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle();
    }

    /// <summary>A plugin switched off before the server is called is refused and nothing is recorded or sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginSwitchedOffBeforeTheCall_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-off-before", fixture.WebsitesPluginId);
        await fixture.SwitchAsync(fixture.WebsitesPluginId, enabled: false);
        try
        {
            var failure = await RefusalAsync(
                PluginHostAccessFixture.ClientCall(fixture.WebsitesPluginId, owner.ClientId), owner.ServiceId, Spec("app.site-off-before.example.test"));

            failure.Should().Be(PluginHostAccessFailure.NotAllowed);
            fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
            (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().BeEmpty();
        }
        finally
        {
            await fixture.SwitchAsync(fixture.WebsitesPluginId, enabled: true);
        }
    }

    /// <summary>
    /// A plugin switched off while its website is being set up gets nothing: the answer is NotAllowed and the website that
    /// reached the server is taken back and recorded as removed, so it does not outlive the plugin.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginSwitchedOffDuringTheCall_GetsNothingAndTheWebsiteIsTakenBackAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("site-off-during", fixture.WebsitesPluginId);
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);
        agent.WhileEnsuring = () => fixture.SwitchAsync(fixture.WebsitesPluginId, enabled: false);
        try
        {
            var failure = await RefusalAsync(
                PluginHostAccessFixture.ClientCall(fixture.WebsitesPluginId, owner.ClientId), owner.ServiceId, Spec("app.site-off-during.example.test"));

            failure.Should().Be(PluginHostAccessFailure.NotAllowed);
            agent.WebsiteOf("web", "app.site-off-during.example.test").Should().BeNull();
            (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Removed);
        }
        finally
        {
            agent.WhileEnsuring = null;
            await fixture.SwitchAsync(fixture.WebsitesPluginId, enabled: true);
        }
    }

    /// <summary>The host name and the account are the service's own: nothing the plugin sends can name another account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WebsiteIsAlwaysSetUpOnTheServicesOwnServerAndAccountAsync()
    {
        var first = await fixture.OwnerWithDomainBoundToAsync("site-own-a", fixture.HostingPluginId);
        var second = await fixture.OwnerWithDomainBoundToAsync("site-own-b", fixture.HostingPluginId);

        await EnsureAsync(first, fixture.HostingPluginId, Spec("app.site-own-a.example.test"));

        fixture.HostingAgents().AgentFor(first.ServerId).Calls.Should().ContainSingle().Which.Should().Be("ensure-website:web:app.site-own-a.example.test");
        fixture.HostingAgents().AgentFor(second.ServerId).Calls.Should().BeEmpty();
        (await fixture.WebsitesOfAsync(second.ServiceId)).Should().BeEmpty();
    }

    /// <summary>A specification with the usual shape.</summary>
    /// <param name="hostname">The host name.</param>
    /// <returns>A specification with PHP 8.3 and a document root that may be a link.</returns>
    private static PluginWebsiteSpec Spec(string hostname)
    {
        return new PluginWebsiteSpec(hostname, "sites/app", "8.3", true);
    }

    /// <summary>Sets a website up as a plugin does and returns the answer.</summary>
    /// <param name="owner">The service's owner scenario.</param>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="spec">The website to set up.</param>
    /// <returns>The website.</returns>
    private Task<PluginWebsite> EnsureAsync(SshOwnerScenario owner, string pluginId, PluginWebsiteSpec spec)
    {
        return fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(pluginId, owner.ClientId),
            api => api.EnsureWebsiteAsync(owner.ServiceId, spec, CancellationToken.None));
    }

    /// <summary>Sets a website up and returns the refusal the plugin catches.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="spec">The website to set up.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(PluginCallBinding binding, int serviceId, PluginWebsiteSpec spec)
    {
        return await fixture.AsHostingPluginAsync(binding, async api =>
        {
            var act = () => api.EnsureWebsiteAsync(serviceId, spec, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
    }

    /// <summary>Sets a website up and answers the refusal, or <see langword="null"/> when it was set up.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="spec">The website to set up.</param>
    /// <returns>The failure the host reported, if any.</returns>
    private async Task<PluginHostAccessFailure?> TryEnsureAsync(PluginCallBinding binding, int serviceId, PluginWebsiteSpec spec)
    {
        try
        {
            await fixture.AsHostingPluginAsync(binding, api => api.EnsureWebsiteAsync(serviceId, spec, CancellationToken.None));
            return null;
        }
        catch (PluginHostAccessException ex)
        {
            return ex.Failure;
        }
    }
}
