using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>Plan changes on <see cref="ClientService"/>.</summary>
public sealed class ClientServicePlanChangeTests
{
    /// <summary>The moment the service in these tests starts billing from and every method is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an active monthly service on product 1.</summary>
    /// <returns>The activated service.</returns>
    private static ClientService Active()
    {
        var service = ClientService.Create(1, 1, BillingCycle.Monthly, Now);
        service.Activate("ref", Now, Now);
        return service;
    }

    /// <summary>The plan and the recurring price change; the cycle and the renewal date stay.</summary>
    [Fact]
    public void ChangePlan_MovesTheProductAndThePrice_AndKeepsTheCycle()
    {
        var service = Active();
        var renewal = service.NextRenewalAt;

        service.ChangePlan(2, 19.99m);

        service.ProductId.Should().Be(2);
        service.RecurringAmount.Should().Be(19.99m);
        service.BillingCycle.Should().Be(BillingCycle.Monthly);
        service.NextRenewalAt.Should().Be(renewal);
    }

    /// <summary>A service that is not active (pending, suspended, terminated) keeps its plan.</summary>
    [Fact]
    public void ChangePlan_OnAPendingService_Throws()
    {
        var service = ClientService.Create(1, 1, BillingCycle.Monthly, Now);

        var act = () => service.ChangePlan(2, 5m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceNotActive);
        service.ProductId.Should().Be(1);
    }

    /// <summary>A negative price is refused.</summary>
    [Fact]
    public void ChangePlan_WithANegativePrice_Throws()
    {
        var service = Active();

        var act = () => service.ChangePlan(2, -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
