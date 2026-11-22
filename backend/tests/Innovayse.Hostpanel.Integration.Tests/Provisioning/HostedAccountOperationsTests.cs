using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <c>HostedAccountOperations</c>, the one place suspend, unsuspend and terminate reach a hosting
/// panel from. Each test resolves it from a real host whose provisioning factory hands back a
/// recording or failing stand-in for the one port this platform does not own, the panel.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class HostedAccountOperationsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose provisioning factory always hands back the given fake.</summary>
    /// <param name="provider">The fake every server resolves to.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostWith(FakeProvisioningProvider provider) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IProvisioningProviderFactory>(new FakeProvisioningProviderFactory(provider))));

    /// <summary>Suspend sends the order to the panel and reports the account updated.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_ForAnActiveService_SendsTheOrderAndReportsUpdatedAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var (_, serverId, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();

        var outcome = await operations.SuspendAsync(
            new HostedAccountSuspension(new HostedAccountTarget(serviceId, serverId, "provisioning-ref-1"), "non-payment"),
            CancellationToken.None);

        outcome.Should().Be(HostedAccountOutcome.Updated);
        var call = provider.Suspended.Should().ContainSingle().Which;
        call.ProvisioningRef.Should().Be("provisioning-ref-1");
        call.Reason.Should().Be("non-payment");
    }

    /// <summary>Unsuspend sends the reference to the panel and reports the account updated.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspend_ForAnActiveService_SendsTheReferenceAndReportsUpdatedAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var (_, serverId, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();

        var outcome = await operations.UnsuspendAsync(
            new HostedAccountUnsuspension(new HostedAccountTarget(serviceId, serverId, "provisioning-ref-1")),
            CancellationToken.None);

        outcome.Should().Be(HostedAccountOutcome.Updated);
        provider.Unsuspended.Should().ContainSingle().Which.Should().Be("provisioning-ref-1");
    }

    /// <summary>Terminate sends the order to the panel and reports the account updated.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_ForAnActiveService_SendsTheOrderAndReportsUpdatedAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var (_, serverId, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();

        var outcome = await operations.TerminateAsync(
            new HostedAccountTermination(new HostedAccountTarget(serviceId, serverId, "provisioning-ref-1"), "cancelled"),
            CancellationToken.None);

        outcome.Should().Be(HostedAccountOutcome.Updated);
        provider.Terminated.Should().ContainSingle().Which.Reason.Should().Be("cancelled");
    }

    /// <summary>A service that was never provisioned has no panel to tell.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_WithNoProvisioningReference_ReportsNoAccountAndCallsNothingAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var (_, serverId, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();

        var outcome = await operations.SuspendAsync(
            new HostedAccountSuspension(new HostedAccountTarget(serviceId, serverId, null), "non-payment"),
            CancellationToken.None);

        outcome.Should().Be(HostedAccountOutcome.NoAccount);
        provider.Suspended.Should().BeEmpty();
    }

    /// <summary>A service with no server assigned has no panel to tell.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_WithNoServer_ReportsNoAccountAndCallsNothingAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();

        var outcome = await operations.TerminateAsync(
            new HostedAccountTermination(new HostedAccountTarget(1, null, "provisioning-ref-1"), "cancelled"),
            CancellationToken.None);

        outcome.Should().Be(HostedAccountOutcome.NoAccount);
        provider.Terminated.Should().BeEmpty();
    }

    /// <summary>A panel that fails is reported as failed, not thrown at the caller.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_WhenThePanelFails_ReportsFailedAsync()
    {
        var host = HostWith(new FakeProvisioningProvider { ShouldFail = true });
        using var scope = host.Services.CreateScope();
        var (_, serverId, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();

        var outcome = await operations.SuspendAsync(
            new HostedAccountSuspension(new HostedAccountTarget(serviceId, serverId, "provisioning-ref-1"), "non-payment"),
            CancellationToken.None);

        outcome.Should().Be(HostedAccountOutcome.Failed);
    }

    /// <summary>
    /// A panel that refuses with its own code is still reported as failed, and the code is logged, so whoever chases the account that
    /// is out of step sees why the panel refused.
    /// </summary>
    /// <param name="operation">Which operation is run.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(HostedAccountOperation.Suspend)]
    [InlineData(HostedAccountOperation.Unsuspend)]
    [InlineData(HostedAccountOperation.Terminate)]
    public async Task PanelRefusal_IsReportedFailedAndItsCodeIsLoggedAsync(HostedAccountOperation operation)
    {
        var logs = new RecordingLoggerProvider();
        var provider = new FakeProvisioningProvider { FailWith = new DomainRuleException(HostpanelErrorCodes.PANEL_ACCESS_DENIED, "the panel refused") };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IProvisioningProviderFactory>(new FakeProvisioningProviderFactory(provider));
                services.AddLogging(logging => logging.AddProvider(logs));
            }));
        using var scope = host.Services.CreateScope();
        var (_, serverId, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        var operations = scope.ServiceProvider.GetRequiredService<IHostedAccountOperations>();
        var target = new HostedAccountTarget(serviceId, serverId, "provisioning-ref-1");

        var outcome = operation switch
        {
            HostedAccountOperation.Suspend => await operations.SuspendAsync(new HostedAccountSuspension(target, "non-payment"), CancellationToken.None),
            HostedAccountOperation.Unsuspend => await operations.UnsuspendAsync(new HostedAccountUnsuspension(target), CancellationToken.None),
            _ => await operations.TerminateAsync(new HostedAccountTermination(target, "cancelled"), CancellationToken.None),
        };

        outcome.Should().Be(HostedAccountOutcome.Failed);
        logs.Entries.Should().Contain(entry => entry.Message.Contains(HostpanelErrorCodes.PANEL_ACCESS_DENIED, StringComparison.Ordinal));
    }
}
