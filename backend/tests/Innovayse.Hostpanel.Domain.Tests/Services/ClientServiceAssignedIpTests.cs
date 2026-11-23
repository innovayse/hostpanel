using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>
/// <see cref="ClientService.RecordAssignedIp"/>: the address a panel reports for the service's account is kept on the service
/// without touching the rest of the account's record.
/// </summary>
public sealed class ClientServiceAssignedIpTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an active service with a provisioning reference.</summary>
    /// <returns>The service.</returns>
    private static ClientService ActiveService()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.Activate("panel-acc-1", Now, Now);
        return service;
    }

    /// <summary>A new service has no assigned address until a panel reports one.</summary>
    [Fact]
    public void DedicatedIp_NewService_IsNull()
    {
        ActiveService().DedicatedIp.Should().BeNull();
    }

    /// <summary>An address is stored on the service.</summary>
    [Fact]
    public void RecordAssignedIp_StoresTheAddress()
    {
        var service = ActiveService();

        service.RecordAssignedIp("203.0.113.9");

        service.DedicatedIp.Should().Be("203.0.113.9");
    }

    /// <summary>A later answer replaces the address that was stored.</summary>
    [Fact]
    public void RecordAssignedIp_ReplacesThePreviousAddress()
    {
        var service = ActiveService();
        service.RecordAssignedIp("203.0.113.9");

        service.RecordAssignedIp("2001:db8::1");

        service.DedicatedIp.Should().Be("2001:db8::1");
    }

    /// <summary>Recording the address changes nothing else of the account or its status.</summary>
    [Fact]
    public void RecordAssignedIp_LeavesTheRestOfTheAccountAlone()
    {
        var service = ActiveService();

        service.RecordAssignedIp("203.0.113.9");

        service.Status.Should().Be(ServiceStatus.Active);
        service.ProvisioningRef.Should().Be("panel-acc-1");
        service.Username.Should().BeNull();
        service.Password.Should().BeNull();
    }
}
