using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PanelPluginCaller"/> over the real activator and container: an activated plugin runs the call and is built with the
/// host's panel transport; every way a plugin can fail to activate answers <c>PANEL_PLUGIN_UNAVAILABLE</c>; a throwing plugin becomes
/// a <see cref="PanelPluginCallException"/> that carries none of its text; cancellation passes through untouched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PanelPluginCallerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An activated plugin runs the call, and the plugin was built with the host's <see cref="PanelTransport"/>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunAsync_ActivatedPlugin_RunsTheCallWithTheHostTransportAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Test = PanelConnectionTestResult.Connected("9.9", 12);
        var context = new PanelTestConnectionContext(harness.Connection.ToPanelConnection(null));

        var answer = await harness.Caller.RunAsync<IProvisioningPanelPlugin, PanelConnectionTestResult>(
            harness.PluginId, "TestConnectionAsync", (plugin, token) => plugin.TestConnectionAsync(context, token), CancellationToken.None);

        answer.AccountCount.Should().Be(12);
        harness.Script.TransportType.Should().Be<PanelTransport>();
    }

    /// <summary>A plugin that is installed but switched off is unavailable, and the plugin is never built.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunAsync_SwitchedOffPlugin_IsUnavailableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), enabled: false);

        var act = () => RunTestAsync(harness, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>A plugin id the registry does not hold is unavailable too.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunAsync_UnknownPlugin_IsUnavailableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        var act = () => harness.Caller.RunAsync<IProvisioningPanelPlugin, int>(
            "not-installed", "TestConnectionAsync", (_, _) => Task.FromResult(1), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>A plugin that declares a capability it does not implement is unavailable: the activator refuses it for every contract.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunAsync_PluginDeclaringALoginCapabilityItLacks_IsUnavailableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeLoginlessPanelPlugin));
        var lying = PanelPluginHarness.Plugin(
            harness.PluginId, typeof(FakeLoginlessPanelPlugin), [harness.Module],
            [PluginCapabilities.ProvisioningPanel, PluginCapabilities.ProvisioningPanelLogin]);
        var registry = PluginActivationTestSeeds.Registry(lying);
        var activator = PluginActivationTestSeeds.Activator(factory, harness.Db, registry: registry);
        var caller = new PanelPluginCaller(registry, activator, Options.Create(new PluginApiOptions()), NullLogger<PanelPluginCaller>.Instance);

        var act = () => caller.RunAsync<IProvisioningPanelPlugin, int>(harness.PluginId, "x", (_, _) => Task.FromResult(1), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
        (await activator.PrepareAsync<IProvisioningPanelPlugin>(lying, CancellationToken.None)).Outcome
            .Should().Be(PluginActivationOutcome.CapabilityNotImplemented);
    }

    /// <summary>A plugin that throws becomes a <see cref="PanelPluginCallException"/> naming the plugin, the operation and the exception type, and nothing the plugin said.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunAsync_ThrowingPlugin_BecomesAnExceptionWithNoPluginTextAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Throw = new InvalidOperationException("secret-api-key-from-the-panel https://panel.internal/");

        var act = () => RunTestAsync(harness, CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<PanelPluginCallException>()).Which;
        thrown.PluginId.Should().Be(harness.PluginId);
        thrown.Operation.Should().Be("TestConnectionAsync");
        thrown.ExceptionType.Should().Be(nameof(InvalidOperationException));
        thrown.InnerException.Should().BeNull();
        thrown.Message.Should().NotContain("secret-api-key").And.NotContain("panel.internal");
    }

    /// <summary>A cancelled caller sees the cancellation, not a plugin failure.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunAsync_CancelledCaller_SeesTheCancellationAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Throw = new OperationCanceledException();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => RunTestAsync(harness, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// A plugin that outlasts the call time limit fails closed as a <see cref="PanelPluginCallException"/>, whether it honours the token it
    /// was given or ignores it, and a plugin that answers in time is unaffected.
    /// </summary>
    /// <param name="honoursToken">Whether the plugin stops when its token is cancelled.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_PluginPastTheTimeLimit_FailsClosedAsync(bool honoursToken)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        var options = Options.Create(new PluginApiOptions { CallTimeoutSeconds = 1, MaxCallTimeoutSeconds = 1 });
        var caller = new PanelPluginCaller(harness.Registry, harness.Activator, options, NullLogger<PanelPluginCaller>.Instance);
        var never = new TaskCompletionSource<int>();

        var slow = () => caller.RunAsync<IProvisioningPanelPlugin, int>(
            harness.PluginId,
            "TestConnectionAsync",
            async (_, token) =>
            {
                if (honoursToken)
                {
                    await Task.Delay(Timeout.Infinite, token);
                }

                return await never.Task;
            },
            CancellationToken.None);
        var fast = () => caller.RunAsync<IProvisioningPanelPlugin, int>(
            harness.PluginId, "TestConnectionAsync", (_, _) => Task.FromResult(7), CancellationToken.None);

        var thrown = (await slow.Should().ThrowAsync<PanelPluginCallException>()).Which;
        thrown.ExceptionType.Should().Be(nameof(TimeoutException));
        (await fast()).Should().Be(7);
    }

    /// <summary>The caller tells whether a loaded plugin declares a capability.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Declares_ReadsTheManifestAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        harness.Caller.Declares(harness.PluginId, PluginCapabilities.ProvisioningPanelLogin).Should().BeTrue();
        harness.Caller.Declares(harness.PluginId, PluginCapabilities.VirtualServer).Should().BeFalse();
        harness.Caller.Declares("not-installed", PluginCapabilities.ProvisioningPanel).Should().BeFalse();
    }

    /// <summary>Runs the plugin's connection test through the caller.</summary>
    /// <param name="harness">The installed plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugin's answer.</returns>
    private static Task<PanelConnectionTestResult> RunTestAsync(PanelPluginHarness harness, CancellationToken ct)
    {
        var context = new PanelTestConnectionContext(harness.Connection.ToPanelConnection(null));
        return harness.Caller.RunAsync<IProvisioningPanelPlugin, PanelConnectionTestResult>(
            harness.PluginId, "TestConnectionAsync", (plugin, token) => plugin.TestConnectionAsync(context, token), ct);
    }
}
