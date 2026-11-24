using System.Net;
using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Commands.RefreshServiceFromPanel;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="RefreshServiceFromPanelCommand"/>: a service's account is read back from its panel and the IP address it reports is
/// recorded, after every provisioning and on demand. It only reads: nothing is ever written to the panel, and a panel that cannot
/// answer never undoes a provisioning that already succeeded.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RefreshServiceFromPanelTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Provisioning reads the new account back, so the service shows the address the panel serves it on.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Provision_RecordsTheAddressThePanelReportsAsync()
    {
        var provider = new FakeProvisioningProvider { AccountAnswer = Answer("203.0.113.9") };

        var serviceId = await ProvisionAsync(provider);

        var stored = await ReloadAsync(serviceId);
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.DedicatedIp.Should().Be("203.0.113.9");
        provider.AccountStateCalls.Should().Be(1);
    }

    /// <summary>A panel that cannot be read after the account was created leaves the provisioning a success.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Provision_PanelCannotBeReadBack_StillSucceedsAsync()
    {
        var provider = new FakeProvisioningProvider { FailAccountRead = true };

        var serviceId = await ProvisionAsync(provider);

        var stored = await ReloadAsync(serviceId);
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.DedicatedIp.Should().BeNull();
        provider.AccountStateCalls.Should().Be(1);
    }

    /// <summary>A later refresh replaces the address the service held with the one the panel reports now.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refresh_PanelReportsANewAddress_ReplacesTheStoredOneAsync()
    {
        var provider = new FakeProvisioningProvider { AccountAnswer = Answer("203.0.113.9") };
        var serviceId = await ProvisionAsync(provider);
        provider.AccountAnswer = Answer("198.51.100.4");

        var outcome = await RefreshAsync(provider, serviceId);

        outcome.IsSuccess.Should().BeTrue();
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("198.51.100.4");
    }

    /// <summary>An address that is no address, or none at all, leaves the one the service holds as it is.</summary>
    /// <param name="reported">What the panel reports.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    public async Task Refresh_PanelReportsNoUsableAddress_KeepsTheStoredOneAsync(string? reported)
    {
        var provider = new FakeProvisioningProvider { AccountAnswer = Answer("203.0.113.9") };
        var serviceId = await ProvisionAsync(provider);
        provider.AccountAnswer = Answer(reported);

        var outcome = await RefreshAsync(provider, serviceId);

        outcome.IsSuccess.Should().BeTrue();
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("203.0.113.9");
    }

    /// <summary>A refresh never writes to the panel, whatever the panel reports: no suspension, package or password call is made.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refresh_NeverWritesToThePanelAsync()
    {
        var provider = new FakeProvisioningProvider { AccountAnswer = Answer("203.0.113.9") };
        var serviceId = await ProvisionAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Suspended, "other.example.test", "legacy", "198.51.100.4");

        await RefreshAsync(provider, serviceId);

        provider.SuspendCalls.Should().Be(0);
        provider.UnsuspendCalls.Should().Be(0);
        provider.ChangePackageCalls.Should().Be(0);
        provider.ChangePasswordCalls.Should().Be(0);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>A panel that cannot be read answers <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> and leaves the service as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refresh_PanelCannotBeRead_FailsWithServicePanelSyncUpstreamFailedAsync()
    {
        var provider = new FakeProvisioningProvider { AccountAnswer = Answer("203.0.113.9") };
        var serviceId = await ProvisionAsync(provider);
        provider.FailAccountRead = true;

        var outcome = await RefreshAsync(provider, serviceId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_PANEL_SYNC_UPSTREAM_FAILED);
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("203.0.113.9");
    }

    /// <summary>A provider that cannot read one account answers its own code, not the generic failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refresh_ProviderCannotReadAnAccount_FailsWithThePanelsCodeAsync()
    {
        var provider = new FakeProvisioningProvider();
        var serviceId = await ProvisionAsync(provider);
        provider.FailWith = new DomainRuleException(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "not supported");

        var outcome = await RefreshAsync(provider, serviceId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
    }

    /// <summary>An id no service takes is <c>SERVICE_NOT_FOUND</c>, and a service never provisioned is <c>SERVICE_NOT_PROVISIONED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refresh_UnknownOrUnprovisionedService_IsRefusedAsync()
    {
        var provider = new FakeProvisioningProvider();
        var pendingId = await ProvisioningTestSeeds.SeedServiceAsync(factory, factory.CreateClient(), await factory.GetAdminTokenAsync());

        (await RefreshAsync(provider, ProvisioningTestSeeds.NoSuchServiceId)).ErrorCode
            .Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        (await RefreshAsync(provider, pendingId)).ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_PROVISIONED);
        provider.AccountStateCalls.Should().Be(0);
    }

    /// <summary>A non-positive id is refused by the validator before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Refresh_NonPositiveId_IsRefusedByTheValidatorAsync()
    {
        var provider = new FakeProvisioningProvider();

        var act = () => RefreshAsync(provider, 0);

        await act.Should().ThrowAsync<ValidationException>();
    }

    /// <summary>Builds what a panel reports for an active account whose address is the one given.</summary>
    /// <param name="address">The address the panel reports.</param>
    /// <returns>The account state.</returns>
    private static HostedAccountState Answer(string? address)
    {
        return new HostedAccountState(HostedAccountStatus.Active, null, null, address);
    }

    /// <summary>Seeds a service and provisions it through <paramref name="provider"/>, as the staff route does.</summary>
    /// <param name="provider">The fake the host provisions through.</param>
    /// <returns>The provisioned service's id.</returns>
    private async Task<int> ProvisionAsync(FakeProvisioningProvider provider)
    {
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, factory.CreateClient(), await factory.GetAdminTokenAsync());
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return serviceId;
    }

    /// <summary>Dispatches the command on a host that resolves every panel to <paramref name="provider"/>.</summary>
    /// <param name="provider">The fake the host reads the panel through.</param>
    /// <param name="serviceId">The service to refresh.</param>
    /// <returns>The command's answer.</returns>
    private async Task<Result> RefreshAsync(FakeProvisioningProvider provider, int serviceId)
    {
        using var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new RefreshServiceFromPanelCommand(serviceId), CancellationToken.None);
    }

    /// <summary>Reads a service back from the database in a fresh scope.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The stored service.</returns>
    private async Task<ClientService> ReloadAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
    }
}
