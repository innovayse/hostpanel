using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="ServerConnectionTester"/> for a server whose module a panel plugin serves: the plugin's connection test runs with the
/// stored server (password included), each answer becomes the result the admin screen and the account count cron read, and a plugin
/// that is off, throwing or doubled never throws out of the tester.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServerConnectionTesterPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A connected plugin answers a successful result with the version and account count the plugin reported.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_Connected_CarriesTheVersionAndTheAccountCountAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Test = PanelConnectionTestResult.Connected("1.2.3", 41);

        var result = await Tester(harness).TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeTrue();
        result.AccountsCount.Should().Be(41);
        result.Version.Should().Be("1.2.3");
        result.ErrorCode.Should().BeNull();
    }

    /// <summary>The plugin receives the stored server, with its password and SSL choice and no port.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_HandsThePluginTheStoredServerAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        await Tester(harness).TestAsync(harness.Server, CancellationToken.None);

        var server = harness.Script.Received.Single().Should().BeOfType<PanelTestConnectionContext>().Which.Server;
        server.ServerId.Should().Be(harness.Server.Id);
        server.Password.Should().Be(PanelPluginHarness.ServerPassword);
        server.UseSsl.Should().BeTrue();
        server.Port.Should().BeNull();
    }

    /// <summary>A panel that reports no account count leaves the count unknown rather than zero.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_ConnectedWithoutACount_LeavesTheCountUnknownAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Test = PanelConnectionTestResult.Connected(null, null);

        var result = await Tester(harness).TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeTrue();
        result.AccountsCount.Should().BeNull();
    }

    /// <summary>Each way the connection can fail is its own code, never a thrown exception.</summary>
    /// <param name="outcome">What the plugin reports.</param>
    /// <param name="code">The code the result carries.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PanelConnectionOutcome.NotConfigured, HostpanelErrorCodes.SERVER_CONNECTION_NOT_CONFIGURED)]
    [InlineData(PanelConnectionOutcome.AuthenticationFailed, HostpanelErrorCodes.SERVER_CONNECTION_AUTH_FAILED)]
    [InlineData(PanelConnectionOutcome.Unreachable, HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE)]
    public async Task TestAsync_Failed_CarriesItsCodeAsync(PanelConnectionOutcome outcome, string code)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Test = PanelConnectionTestResult.Failed(outcome);

        var result = await Tester(harness).TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(code);
    }

    /// <summary>A plugin that is switched off fails the test with <c>PANEL_PLUGIN_UNAVAILABLE</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_SwitchedOffPlugin_IsUnavailableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), enabled: false);

        var result = await Tester(harness).TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>A plugin that throws fails the test as unreachable and nothing it said reaches the result.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_ThrowingPlugin_IsUnreachableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.Throw = new InvalidOperationException("secret panel text");

        var result = await Tester(harness).TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        result.Version.Should().BeNull();
    }

    /// <summary>Two installed plugins declaring the module fail the test with <c>PANEL_PLUGIN_CONFLICT</c>, and neither is called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_TwoPluginsServingTheModule_IsAConflictAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        var registry = PluginActivationTestSeeds.Registry(
            PanelPluginHarness.Plugin("first", typeof(FakePanelPlugin), [harness.Module], [PluginCapabilities.ProvisioningPanel]),
            PanelPluginHarness.Plugin("second", typeof(FakePanelPlugin), [harness.Module.ToUpperInvariant()], [PluginCapabilities.ProvisioningPanel]));
        var tester = Tester(harness, new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance));

        var result = await tester.TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT);
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>A module neither a plugin nor the host tests (here, the plugin that served it is not installed) is answered as an unavailable panel, the code every provisioning call answers.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_ModuleNobodyServes_IsUnavailableAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        var tester = Tester(harness, new ServerModulePluginResolver(PluginActivationTestSeeds.Registry(), NullLogger<ServerModulePluginResolver>.Instance));

        var result = await tester.TestAsync(harness.Server, CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>Builds the tester over the harness's real registry, activator and caller.</summary>
    /// <param name="harness">The installed plugin.</param>
    /// <param name="resolver">The resolver to use instead of the harness's own, or null.</param>
    /// <returns>The tester.</returns>
    private static ServerConnectionTester Tester(PanelPluginHarness harness, ServerModulePluginResolver? resolver = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        return new ServerConnectionTester(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<ILoggerFactory>(),
            Options.Create(new ProvisioningOptions()),
            NullLogger<ServerConnectionTester>.Instance,
            resolver ?? harness.Resolver,
            harness.Caller);
    }
}
