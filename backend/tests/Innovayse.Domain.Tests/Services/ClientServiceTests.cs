namespace Innovayse.Domain.Tests.Services;

using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Events;

/// <summary>Unit tests for the <see cref="ClientService"/> aggregate.</summary>
public class ClientServiceTests
{
    /// <summary>Verifies that <see cref="ClientService.Create"/> sets Pending status and raises the created event.</summary>
    [Fact]
    public void Create_SetsPendingStatus_AndRaisesEvent()
    {
        var svc = ClientService.Create(clientId: 1, productId: 2, billingCycle: "monthly");

        Assert.Equal(1, svc.ClientId);
        Assert.Equal(2, svc.ProductId);
        Assert.Equal("monthly", svc.BillingCycle);
        Assert.Equal(ServiceStatus.Pending, svc.Status);
        Assert.Single(svc.DomainEvents);
        Assert.IsType<ClientServiceCreatedEvent>(svc.DomainEvents[0]);
    }

    /// <summary>Verifies that <see cref="ClientService.Activate"/> transitions status to Active and stores the provisioning ref.</summary>
    [Fact]
    public void Activate_ChangesStatusToActive()
    {
        var svc = ClientService.Create(1, 2, "monthly");
        svc.Activate("cpanel-acc-123");
        Assert.Equal(ServiceStatus.Active, svc.Status);
        Assert.Equal("cpanel-acc-123", svc.ProvisioningRef);
    }

    /// <summary>Verifies that <see cref="ClientService.Suspend"/> transitions status to Suspended and raises the suspended event.</summary>
    [Fact]
    public void Suspend_ChangeStatusToSuspended_AndRaisesEvent()
    {
        var svc = ClientService.Create(1, 2, "monthly");
        svc.Activate("ref");
        svc.Suspend();
        Assert.Equal(ServiceStatus.Suspended, svc.Status);
        Assert.Contains(svc.DomainEvents, e => e is ClientServiceSuspendedEvent);
    }

    /// <summary>Verifies that <see cref="ClientService.Terminate"/> transitions status to Terminated and raises the terminated event.</summary>
    [Fact]
    public void Terminate_ChangesStatusToTerminated_AndRaisesEvent()
    {
        var svc = ClientService.Create(1, 2, "monthly");
        svc.Activate("ref");
        svc.Terminate();
        Assert.Equal(ServiceStatus.Terminated, svc.Status);
        Assert.Contains(svc.DomainEvents, e => e is ClientServiceTerminatedEvent);
    }

    /// <summary>Verifies that <see cref="ClientService.Activate"/> sets NextRenewalAt for monthly and annual billing cycles.</summary>
    [Fact]
    public void Activate_SetsNextRenewalAt_ForMonthlyAndAnnual()
    {
        var monthlySvc = ClientService.Create(1, 2, "monthly");
        monthlySvc.Activate("ref-m");
        Assert.NotNull(monthlySvc.NextRenewalAt);
        Assert.True(monthlySvc.NextRenewalAt > DateTimeOffset.UtcNow);

        var annualSvc = ClientService.Create(1, 2, "annual");
        annualSvc.Activate("ref-a");
        Assert.NotNull(annualSvc.NextRenewalAt);
        Assert.True(annualSvc.NextRenewalAt > monthlySvc.NextRenewalAt);
    }
}
