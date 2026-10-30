using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>
/// <see cref="ClientService.ActivateDigital"/>: a digital service turns Active and starts billing without a
/// provisioning reference, a server or the provisioning event, under the same Pending rule as
/// <see cref="ClientService.Activate"/>.
/// </summary>
public sealed class ClientServiceDigitalActivationTests
{
    /// <summary>The moment every service in these tests starts billing from.</summary>
    private static readonly DateTimeOffset BillingAnchor = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    /// <summary>The fixed moment every service in these tests is ordered at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A pending digital service turns Active, with its first renewal one cycle after the anchor.</summary>
    [Fact]
    public void ActivateDigital_Pending_TurnsActiveAndStartsBilling()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        service.ActivateDigital(BillingAnchor);

        service.Status.Should().Be(ServiceStatus.Active);
        service.NextRenewalAt.Should().Be(BillingAnchor.AddMonths(1));
    }

    /// <summary>Nothing was provisioned: no reference, no server, and no provisioning event that would send the hosting account mail.</summary>
    [Fact]
    public void ActivateDigital_ProvisionsNothing()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Annual, Now);

        service.ActivateDigital(BillingAnchor);

        service.ProvisioningRef.Should().BeNull();
        service.ServerId.Should().BeNull();
        service.DomainEvents.OfType<ServiceProvisionedEvent>().Should().BeEmpty();
    }

    /// <summary>A renewal date staff already set later than the first period's end is kept, as with a provisioned activation.</summary>
    [Fact]
    public void ActivateDigital_KeepsALaterRenewalDate()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var later = BillingAnchor.AddMonths(6);
        service.ExtendRenewalTo(later);

        service.ActivateDigital(BillingAnchor);

        service.NextRenewalAt.Should().Be(later);
    }

    /// <summary>Only a Pending service activates: an active one is refused with the not-pending code and left as it was.</summary>
    [Fact]
    public void ActivateDigital_NotPending_IsRefused()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.ActivateDigital(BillingAnchor);
        var renewal = service.NextRenewalAt;

        var act = () => service.ActivateDigital(BillingAnchor.AddMonths(3));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceNotPending);
        service.Status.Should().Be(ServiceStatus.Active);
        service.NextRenewalAt.Should().Be(renewal);
    }
}
