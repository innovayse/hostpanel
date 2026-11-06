using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>
/// <see cref="ClientService.HoldsHostingAccount"/>: whether a service has a hosting account on its
/// panel to ask about, which a provisioning reference alone does not say.
/// </summary>
public sealed class ClientServiceHostingAccountTests
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

    /// <summary>A service that was never provisioned holds no account.</summary>
    [Fact]
    public void HoldsHostingAccount_NeverProvisioned_IsFalse()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        service.HoldsHostingAccount.Should().BeFalse();
    }

    /// <summary>An active service with a reference holds its account.</summary>
    [Fact]
    public void HoldsHostingAccount_Active_IsTrue()
    {
        ActiveService().HoldsHostingAccount.Should().BeTrue();
    }

    /// <summary>A suspended service still holds its account.</summary>
    [Fact]
    public void HoldsHostingAccount_Suspended_IsTrue()
    {
        var service = ActiveService();
        service.Suspend(Now, hostingAlreadyUpdated: true);

        service.HoldsHostingAccount.Should().BeTrue();
    }

    /// <summary>A terminated service keeps its reference for the record but holds no account.</summary>
    [Fact]
    public void HoldsHostingAccount_Terminated_IsFalseAndKeepsTheReference()
    {
        var service = ActiveService();
        service.Terminate(Now, "closed", hostingAlreadyUpdated: true);

        service.HoldsHostingAccount.Should().BeFalse();
        service.ProvisioningRef.Should().Be("panel-acc-1");
    }
}
