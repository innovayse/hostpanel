using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="PanelPluginProvisioningProvider"/> against a fake panel plugin built by the real activator:
/// each operation hands the plugin the context the SDK documents (the stored server with its password and SSL choice and no port, the
/// service id and reference, the client's per plugin reference), and every answer the plugin can give is mapped onto the port the
/// handlers already use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PanelPluginProvisioningProviderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The five operations that answer a <see cref="PanelActionResult"/>, by name, each run against a provider for service 7 and reference <c>acct-7</c>.</summary>
    private static readonly IReadOnlyDictionary<string, Func<PanelPluginProvisioningProvider, Task>> Actions =
        new Dictionary<string, Func<PanelPluginProvisioningProvider, Task>>
        {
            ["suspend"] = p => p.SuspendAsync(new SuspendAccountOrder(7, "acct-7", "Overdue"), CancellationToken.None),
            ["unsuspend"] = p => p.UnsuspendAsync(new UnsuspendAccountOrder(7, "acct-7"), CancellationToken.None),
            ["change-package"] = p => p.ChangePackageAsync(new ChangeAccountPackageOrder(7, "acct-7", "Pro"), CancellationToken.None),
            ["change-password"] = p => p.ChangePasswordAsync(new ChangeAccountPasswordOrder(7, "acct-7", "N3w-secret"), CancellationToken.None),
        };

    /// <summary>A created account answers the reference, and the plugin receives the stored server, the service, the client's reference and the order's details.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_Created_HandsThePluginTheFullContextAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Create = PanelCreateResult.Created("acct-new");

        var result = await harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Created);
        result.ProvisioningRef.Should().Be("acct-new");
        var context = harness.Script.Received.Should().ContainSingle().Which.Should().BeOfType<PanelCreateAccountContext>().Which;
        context.Server.ServerId.Should().Be(harness.Server.Id);
        context.Server.Hostname.Should().Be("panel.example.test");
        context.Server.IpAddress.Should().Be("203.0.113.9");
        context.Server.Port.Should().BeNull("the host keeps no panel port");
        context.Server.UseSsl.Should().BeTrue();
        context.Server.Username.Should().Be("root");
        context.Server.Password.Should().Be(PanelPluginHarness.ServerPassword);
        context.Server.ApiToken.Should().Be("api-token-value");
        context.Server.AccessHash.Should().Be("access-hash-value");
        context.Service.ServiceId.Should().Be(7);
        context.Service.ProvisioningRef.Should().BeNull();
        context.Client.ClientReference.Should().Be(harness.ClientReference(3)).And.NotBe("3");
        context.Client.Email.Should().Be("owner@example.test");
        context.Client.Name.Should().Be("Olga Owner");
        context.Package.Name.Should().Be("Basic");
        context.DomainName.Should().Be("example.com");
        context.Username.Should().Be("alice");
        context.Password.Should().Be("pw-secret-1");
        harness.Script.TransportType.Should().Be<PanelTransport>();
    }

    /// <summary>The contexts the plugin receives print without the password, API token, access hash or the client's contact.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ContextPrintsNoSecretAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        await harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        var text = harness.Script.Received.Single().ToString();
        text.Should().NotContain("pw-secret-1").And.NotContain(PanelPluginHarness.ServerPassword)
            .And.NotContain("api-token-value").And.NotContain("access-hash-value").And.NotContain("owner@example.test");
    }

    /// <summary>An account the panel already holds under the username is answered as already existing, with its domain, so the handler can adopt it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_AlreadyApplied_IsAnExistingAccountAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Create = PanelCreateResult.AlreadyExists("acct-old", "example.com");

        var result = await harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
        result.ProvisioningRef.Should().Be("acct-old");
        result.ExistingAccountDomain.Should().Be("example.com");
    }

    /// <summary>
    /// A panel that cannot name the domain of the account it already holds (the retry after a pending create) is answered with the
    /// service's own domain, so the handler adopts the account instead of leaving the service in a conflict.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_AlreadyAppliedWithNoDomain_IsTheServicesOwnDomainAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Create = PanelCreateResult.AlreadyExists("acct-old", null);

        var result = await harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
        result.ExistingAccountDomain.Should().Be("example.com");
    }

    /// <summary>An account the panel accepted and is still creating is answered as pending, with the reference.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_Pending_IsPendingWithTheReferenceAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Create = PanelCreateResult.Pending("acct-wait");

        var result = await harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Pending);
        result.ProvisioningRef.Should().Be("acct-wait");
    }

    /// <summary>A refusal with a reason throws the reason's code, so the handler answers the caller with its own sentence.</summary>
    /// <param name="reason">The reason the plugin gives.</param>
    /// <param name="code">The code the host answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PanelRefusalReason.PackageUnknown, HostpanelErrorCodes.PANEL_PACKAGE_UNKNOWN)]
    [InlineData(PanelRefusalReason.UsernameInvalid, HostpanelErrorCodes.PANEL_USERNAME_INVALID)]
    [InlineData(PanelRefusalReason.DomainInvalid, HostpanelErrorCodes.PANEL_DOMAIN_INVALID)]
    [InlineData(PanelRefusalReason.PasswordRejected, HostpanelErrorCodes.PANEL_PASSWORD_REJECTED)]
    [InlineData(PanelRefusalReason.LimitReached, HostpanelErrorCodes.PANEL_LIMIT_REACHED)]
    [InlineData(PanelRefusalReason.AccessDenied, HostpanelErrorCodes.PANEL_ACCESS_DENIED)]
    [InlineData(PanelRefusalReason.Other, HostpanelErrorCodes.PANEL_REFUSED)]
    [InlineData(PanelRefusalReason.ImageUnknown, HostpanelErrorCodes.PANEL_REFUSED)]
    [InlineData(PanelRefusalReason.NameInvalid, HostpanelErrorCodes.PANEL_REFUSED)]
    public async Task Provision_Refused_ThrowsTheReasonsCodeAsync(PanelRefusalReason reason, string code)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Create = PanelCreateResult.Refused(reason);

        var act = () => harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(code);
    }

    /// <summary>An answer a create cannot give (not found, not supported), or a success without a reference, is a refused result the handler logs.</summary>
    /// <param name="outcome">The outcome the plugin answers.</param>
    /// <param name="reference">The reference it names, or null.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PanelOperationOutcome.NotFound, "acct-1")]
    [InlineData(PanelOperationOutcome.NotSupported, "acct-1")]
    [InlineData(PanelOperationOutcome.Succeeded, null)]
    [InlineData(PanelOperationOutcome.Pending, " ")]
    public async Task Provision_UnusableAnswer_IsRefusedAsync(PanelOperationOutcome outcome, string? reference)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Create = new PanelCreateResult(outcome, reference, null, null, null);

        var result = await harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>A plugin that throws leaves the outcome unknown (the account may exist), which the handler retries.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_PluginThrows_IsAnUnknownOutcomeAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Throw = new InvalidOperationException("boom");

        var act = () => harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PROVISIONING_OUTCOME_UNKNOWN);
    }

    /// <summary>A plugin that is switched off is unavailable: nothing is created and nothing is silently done by another provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_SwitchedOffPlugin_IsUnavailableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), enabled: false);

        var act = () => harness.Provider().ProvisionAsync(Order(), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>
    /// The server's stored password is read only after the plugin is resolved and available: a plugin that is off is refused before the
    /// database is touched (the context here is disposed, so a read would throw something else).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_SwitchedOffPlugin_IsRefusedBeforeThePasswordIsReadAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), enabled: false);
        using var scope = factory.Services.CreateScope();
        var closed = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await closed.DisposeAsync();
        var provider = new PanelPluginProvisioningProvider(
            harness.PluginId,
            harness.Connection,
            harness.Caller,
            new PanelConnectionReader(closed),
            scope.ServiceProvider.GetRequiredService<IPluginReferences>(),
            NullLogger<PanelPluginProvisioningProvider>.Instance);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "acct-7", "Overdue"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>Done, already done and accepted answers return normally for every account action, and the plugin got the service and reference.</summary>
    /// <param name="action">The action's name.</param>
    /// <param name="outcome">The outcome the plugin answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("suspend", PanelOperationOutcome.Succeeded)]
    [InlineData("suspend", PanelOperationOutcome.AlreadyApplied)]
    [InlineData("suspend", PanelOperationOutcome.Pending)]
    [InlineData("unsuspend", PanelOperationOutcome.Succeeded)]
    [InlineData("unsuspend", PanelOperationOutcome.AlreadyApplied)]
    [InlineData("unsuspend", PanelOperationOutcome.Pending)]
    [InlineData("change-package", PanelOperationOutcome.Succeeded)]
    [InlineData("change-package", PanelOperationOutcome.AlreadyApplied)]
    [InlineData("change-package", PanelOperationOutcome.Pending)]
    [InlineData("change-password", PanelOperationOutcome.Succeeded)]
    [InlineData("change-password", PanelOperationOutcome.AlreadyApplied)]
    [InlineData("change-password", PanelOperationOutcome.Pending)]
    public async Task Action_DoneAlreadyDoneOrAccepted_ReturnsNormallyAsync(string action, PanelOperationOutcome outcome)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Action = new PanelActionResult(outcome, null);

        await Actions[action](harness.Provider());

        harness.Script.Received.Should().ContainSingle();
        ServiceOf(harness.Script.Received.Single()).Should().Be(new PanelService(7, "acct-7"));
    }

    /// <summary>An account the panel does not have, or an action it does not support, is refused with its own code for every account action.</summary>
    /// <param name="action">The action's name.</param>
    /// <param name="outcome">The outcome the plugin answers.</param>
    /// <param name="code">The code the host answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("suspend", PanelOperationOutcome.NotFound, HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND)]
    [InlineData("unsuspend", PanelOperationOutcome.NotFound, HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND)]
    [InlineData("change-package", PanelOperationOutcome.NotFound, HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND)]
    [InlineData("change-password", PanelOperationOutcome.NotFound, HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND)]
    [InlineData("suspend", PanelOperationOutcome.NotSupported, HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED)]
    [InlineData("change-package", PanelOperationOutcome.NotSupported, HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED)]
    public async Task Action_NotFoundOrNotSupported_IsRefusedWithItsCodeAsync(string action, PanelOperationOutcome outcome, string code)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Action = new PanelActionResult(outcome, null);

        var act = () => Actions[action](harness.Provider());

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(code);
    }

    /// <summary>A refusal with a reason throws the reason's code for every account action.</summary>
    /// <param name="action">The action's name.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("suspend")]
    [InlineData("unsuspend")]
    [InlineData("change-package")]
    [InlineData("change-password")]
    public async Task Action_Refused_ThrowsTheReasonsCodeAsync(string action)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Action = PanelActionResult.Refused(PanelRefusalReason.PasswordRejected);

        var act = () => Actions[action](harness.Provider());

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PASSWORD_REJECTED);
    }

    /// <summary>A plugin that throws is a failure the handler logs, and nothing the plugin said leaves the host.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Action_PluginThrows_IsAPluginCallFailureAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Throw = new InvalidOperationException("panel said no");

        var act = () => Actions["suspend"](harness.Provider());

        (await act.Should().ThrowAsync<PanelPluginCallException>()).Which.Message.Should().NotContain("panel said no");
    }

    /// <summary>Terminating an account the panel no longer has is done: it is the state the caller asked for, so a retry is safe.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_NotFound_CountsAsDoneAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Action = PanelActionResult.NotFound();

        await harness.Provider().TerminateAsync(new TerminateAccountOrder(7, "acct-7", "Cancelled"), CancellationToken.None);

        var context = harness.Script.Received.Single().Should().BeOfType<PanelTerminateContext>().Which;
        context.Service.Should().Be(new PanelService(7, "acct-7"));
        context.Reason.Should().Be("Cancelled");
    }

    /// <summary>Terminate refuses what the panel refuses and returns normally for done, already done and accepted.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_RefusedThrowsAndDoneReturnsAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Action = PanelActionResult.Pending();
        await harness.Provider().TerminateAsync(new TerminateAccountOrder(7, "acct-7", "Cancelled"), CancellationToken.None);
        harness.Script.Action = PanelActionResult.Refused(PanelRefusalReason.AccessDenied);

        var act = () => harness.Provider().TerminateAsync(new TerminateAccountOrder(7, "acct-7", "Cancelled"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_ACCESS_DENIED);
    }

    /// <summary>The package and the new password reach the plugin in their contexts.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePackageAndPassword_HandThePluginTheirValuesAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        await Actions["change-package"](harness.Provider());
        await Actions["change-password"](harness.Provider());

        var received = harness.Script.Received.ToList();
        received[0].Should().BeOfType<PanelChangePackageContext>().Which.Package.Name.Should().Be("Pro");
        received[1].Should().BeOfType<PanelChangePasswordContext>().Which.NewPassword.Should().Be("N3w-secret");
        received[1].ToString().Should().NotContain("N3w-secret");
    }

    /// <summary>A sign in link is returned as an absolute address, and the caller's address reaches the plugin.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SsoUrl_ReturnsTheLinkAndPassesTheCallerAddressAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Sso = PanelSsoResult.Issued("https://panel.example.test:2083/login?t=abc");

        var url = await harness.Provider().GetCPanelSsoUrlAsync(new ControlPanelSsoOrder(7, "acct-7", "203.0.113.50"), CancellationToken.None);

        url.Should().Be("https://panel.example.test:2083/login?t=abc");
        var context = harness.Script.Received.Single().Should().BeOfType<PanelSsoContext>().Which;
        context.CallerIp.Should().Be("203.0.113.50");
        context.Service.Should().Be(new PanelService(7, "acct-7"));
    }

    /// <summary>A link that is not an http or https address is never handed to a browser.</summary>
    /// <param name="url">What the plugin answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/path")]
    [InlineData("ftp://panel.example.test/")]
    public async Task SsoUrl_NotAWebAddress_IsRefusedAsync(string url)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Sso = PanelSsoResult.Issued(url);

        var act = () => harness.Provider().GetCPanelSsoUrlAsync(new ControlPanelSsoOrder(7, "acct-7", null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED);
    }

    /// <summary>A sign in link refused or not supported by the panel answers its own code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SsoUrl_NotSupported_IsRefusedWithItsCodeAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Sso = PanelSsoResult.NotSupported();

        var act = () => harness.Provider().GetCPanelSsoUrlAsync(new ControlPanelSsoOrder(7, "acct-7", null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
    }

    /// <summary>A plugin with the login capability answers the credentials the panel reports.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Credentials_WithTheLoginCapability_AreTheLoginDetailsAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        var credentials = await harness.Provider().GetCredentialsAsync(new AccountCredentialsOrder(7, "acct-7"), CancellationToken.None);

        credentials.Username.Should().Be("acct-1");
        credentials.Password.Should().Be("panel-password");
        credentials.Domain.Should().Be("one.example.test");
        credentials.ServerIp.Should().Be("203.0.113.9");
        credentials.CpanelUrl.Should().Be("https://panel.example.test:2087");
        harness.Script.Received.Single().Should().BeOfType<PanelAccountContext>().Which.Service.Should().Be(new PanelService(7, "acct-7"));
    }

    /// <summary>A plugin without the login capability is never asked for credentials: the host answers that the action is not supported.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Credentials_WithoutTheLoginCapability_AreNotSupportedAndNotAskedAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeLoginlessPanelPlugin));

        var act = () => harness.Provider().GetCredentialsAsync(new AccountCredentialsOrder(7, "acct-7"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>An account the panel does not have answers its own code when its credentials are asked.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Credentials_NotFound_IsRefusedWithItsCodeAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Login = ValueResult<PanelLoginDetails, PanelRefusalReason>.NotFound();

        var act = () => harness.Provider().GetCredentialsAsync(new AccountCredentialsOrder(7, "acct-7"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND);
    }

    /// <summary>
    /// Reading one account hands the plugin the stored server and the service and reference, and maps the panel's answer onto the host's
    /// own state: the status, the domain, the package and the address.
    /// </summary>
    /// <param name="panelStatus">The status the panel reports.</param>
    /// <param name="expected">The host's own status.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PanelAccountStatus.Active, HostedAccountStatus.Active)]
    [InlineData(PanelAccountStatus.Suspended, HostedAccountStatus.Suspended)]
    public async Task GetAccount_Reported_IsMappedAndTheContextCarriesTheReferenceAsync(PanelAccountStatus panelStatus, HostedAccountStatus expected)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Account = ValueResult<PanelAccount, PanelRefusalReason>.Done(
            new PanelAccount("acct-7", "example.com", panelStatus, "Basic", null) { IpAddress = "203.0.113.20" });

        var state = await harness.Provider().GetAccountAsync(new AccountStateOrder(7, "acct-7"), CancellationToken.None);

        state.Should().Be(new HostedAccountState(expected, "example.com", "Basic", "203.0.113.20"));
        var context = harness.Script.Received.Should().ContainSingle().Which.Should().BeOfType<PanelAccountContext>().Which;
        context.Service.Should().Be(new PanelService(7, "acct-7"));
        context.Server.ServerId.Should().Be(harness.Server.Id);
        context.Server.Password.Should().Be(PanelPluginHarness.ServerPassword);
    }

    /// <summary>A field the panel does not report stays unreported, so the sync can say it cannot compare it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetAccount_NothingElseReported_LeavesTheFieldsNullAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Account = ValueResult<PanelAccount, PanelRefusalReason>.Done(
            new PanelAccount("acct-7", null, PanelAccountStatus.Active, null, null));

        var state = await harness.Provider().GetAccountAsync(new AccountStateOrder(7, "acct-7"), CancellationToken.None);

        state.Should().Be(new HostedAccountState(HostedAccountStatus.Active, null, null, null));
    }

    /// <summary>A plugin that does not declare the single account read is not supported and is never called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetAccount_WithoutTheCapability_IsNotSupportedAndNotAskedAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeLoginlessPanelPlugin));

        var act = () => harness.Provider().GetAccountAsync(new AccountStateOrder(7, "acct-7"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>An account the panel does not have, a read it cannot do and a refusal each answer their own code.</summary>
    /// <param name="outcome">How the plugin answers.</param>
    /// <param name="code">The code the host answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PanelOperationOutcome.NotFound, HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND)]
    [InlineData(PanelOperationOutcome.NotSupported, HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED)]
    [InlineData(PanelOperationOutcome.Refused, HostpanelErrorCodes.PANEL_ACCESS_DENIED)]
    public async Task GetAccount_NotAnAccount_IsRefusedWithItsCodeAsync(PanelOperationOutcome outcome, string code)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Account = outcome switch
        {
            PanelOperationOutcome.NotFound => ValueResult<PanelAccount, PanelRefusalReason>.NotFound(),
            PanelOperationOutcome.NotSupported => ValueResult<PanelAccount, PanelRefusalReason>.NotSupported(),
            _ => ValueResult<PanelAccount, PanelRefusalReason>.Refused(PanelRefusalReason.AccessDenied),
        };

        var act = () => harness.Provider().GetAccountAsync(new AccountStateOrder(7, "acct-7"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(code);
    }

    /// <summary>A plugin that throws is a call failure whose text never reaches the caller, and a switched off plugin is unavailable.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetAccount_ThrowingOrOffPlugin_IsAFailureWithoutPluginTextAsync()
    {
        using var throwing = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        throwing.Script.Throw = new InvalidOperationException("plugin internal text");
        using var off = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin), enabled: false);

        var thrown = () => throwing.Provider().GetAccountAsync(new AccountStateOrder(7, "acct-7"), CancellationToken.None);
        var unavailable = () => off.Provider().GetAccountAsync(new AccountStateOrder(7, "acct-7"), CancellationToken.None);

        (await thrown.Should().ThrowAsync<PanelPluginCallException>()).Which.Message.Should().NotContain("plugin internal text");
        (await unavailable.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>Builds the order a service is provisioned with: service 7, client 3.</summary>
    /// <returns>The order.</returns>
    private static ProvisionAccountOrder Order()
    {
        return new ProvisionAccountOrder(7, "example.com", "alice", "pw-secret-1", "Basic", 3, "owner@example.test", "Olga Owner");
    }

    /// <summary>Reads the service out of whichever context an account action sent.</summary>
    /// <param name="context">The context the plugin received.</param>
    /// <returns>The service in it.</returns>
    private static PanelService ServiceOf(object context)
    {
        return context switch
        {
            PanelSuspendContext suspend => suspend.Service,
            PanelAccountContext account => account.Service,
            PanelChangePackageContext package => package.Service,
            PanelChangePasswordContext password => password.Service,
            _ => throw new InvalidOperationException($"Unexpected context {context.GetType().Name}."),
        };
    }
}
