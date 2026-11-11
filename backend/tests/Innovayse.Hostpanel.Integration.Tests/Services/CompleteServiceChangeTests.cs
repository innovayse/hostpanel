using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="CompleteServiceChangeCommand"/> against Postgres: the one path every approved plan change ends in. The panel is
/// asked first, then the plan, the price, the credit and the request move together, and a failure leaves the request Approved
/// with nothing credited so it can be repeated.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteServiceChangeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Dispatches the command on a host whose provisioning provider is <paramref name="provider"/>.</summary>
    /// <param name="provider">The fake panel the host provisions through.</param>
    /// <param name="requestId">The request to apply.</param>
    /// <returns>The use case's answer.</returns>
    private async Task<Result> CompleteOnAsync(FakeProvisioningProvider provider, int requestId)
    {
        var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, provider);
        return await CompleteAsync(host, requestId);
    }

    /// <summary>Dispatches the command on <paramref name="host"/>.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="requestId">The request to apply.</param>
    /// <returns>The use case's answer.</returns>
    private static async Task<Result> CompleteAsync(WebApplicationFactory<Program> host, int requestId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new CompleteServiceChangeCommand(requestId));
    }

    /// <summary>A downgrade of a provisioned service asks the panel for the package, moves the plan and credits the balance once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADowngrade_AsksThePanel_ChangesThePlan_AndCreditsTheBalance()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m, provisioned: true);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        var provider = new FakeProvisioningProvider();

        var outcome = await CompleteOnAsync(provider, requestId);

        outcome.IsSuccess.Should().BeTrue();
        provider.ChangePackageCalls.Should().Be(1);
        var service = await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId);
        service.ProductId.Should().Be(seeded.ToProductId);
        service.RecurringAmount.Should().Be(10m);
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        request.Status.Should().Be(ServiceChangeStatus.Completed);
        request.ResolvedAt.Should().NotBeNull();
        var credited = ServiceChangeTestSeeds.ExpectedProration(20m, 10m, seeded.NextRenewalAt);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, seeded.ClientId)).Should().Be(credited);
        (await ServiceChangeTestSeeds.PlanChangeCreditLinesOfAsync(factory, seeded.ClientId)).Should().Be(1);
    }

    /// <summary>A service with no hosting account has no panel to ask; only its record changes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AServiceWithoutAHostingAccount_DoesNotAskThePanel()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        var provider = new FakeProvisioningProvider();

        var outcome = await CompleteOnAsync(provider, requestId);

        outcome.IsSuccess.Should().BeTrue();
        provider.ChangePackageCalls.Should().Be(0);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.ToProductId);
    }

    /// <summary>
    /// A panel that refuses leaves the request Approved with nothing changed or credited; repeating the call on a working
    /// panel applies it once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenThePanelRefuses_NothingChanges_AndTheRetryAppliesItOnce()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m, provisioned: true);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);

        var refused = await CompleteOnAsync(new FakeProvisioningProvider { ShouldFail = true }, requestId);

        refused.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Approved);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, seeded.ClientId)).Should().Be(0m);

        var retried = await CompleteOnAsync(new FakeProvisioningProvider(), requestId);

        retried.IsSuccess.Should().BeTrue();
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.ToProductId);
        (await ServiceChangeTestSeeds.PlanChangeCreditLinesOfAsync(factory, seeded.ClientId)).Should().Be(1);
    }

    /// <summary>Completing a request that is already Completed answers success and credits nothing more.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACompletedRequest_IsANoOp_AndCreditsOnlyOnce()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        var provider = new FakeProvisioningProvider();

        (await CompleteOnAsync(provider, requestId)).IsSuccess.Should().BeTrue();
        var again = await CompleteOnAsync(provider, requestId);

        again.IsSuccess.Should().BeTrue();
        (await ServiceChangeTestSeeds.PlanChangeCreditLinesOfAsync(factory, seeded.ClientId)).Should().Be(1);
    }

    /// <summary>A request nobody approved is not applied.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APendingRequest_IsRefusedWithInvalidState()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);

        var outcome = await CompleteOnAsync(new FakeProvisioningProvider(), requestId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
    }

    /// <summary>A rejected request is not applied.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARejectedRequest_IsRefusedWithInvalidState()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Rejected);

        var outcome = await CompleteOnAsync(new FakeProvisioningProvider(), requestId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
    }

    /// <summary>An id that names no request is a missing request.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownRequest_IsRefusedWithNotFound()
    {
        var outcome = await CompleteOnAsync(new FakeProvisioningProvider(), 999_999_999);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_REQUEST_NOT_FOUND);
    }

    /// <summary>An upgrade whose proration invoice is still unpaid is not applied.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUpgradeWhoseInvoiceIsUnpaid_IsRefusedWithInvoiceUnpaid()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);

        var outcome = await CompleteOnAsync(new FakeProvisioningProvider(), requestId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_INVOICE_UNPAID);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Approved);
    }

    /// <summary>A service staff moved to another plan since the request was made is not moved again from a plan it left.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AServiceThatLeftItsPlan_IsRefusedWithInvalidState()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.ClientServices.FirstAsync(s => s.Id == seeded.ServiceId)).ChangeProduct(seeded.ToProductId);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var outcome = await CompleteOnAsync(new FakeProvisioningProvider(), requestId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, seeded.ClientId)).Should().Be(0m);
    }

    /// <summary>A service that was suspended meanwhile keeps its plan until it is active again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASuspendedService_IsRefusedWithServiceNotActive()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.ClientServices.FirstAsync(s => s.Id == seeded.ServiceId)).Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var outcome = await CompleteOnAsync(new FakeProvisioningProvider(), requestId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
    }

    /// <summary>A provisioned service cannot move to a plan that names no hosting package.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APlanWithoutAHostingPackage_IsRefusedForAProvisionedService()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m, provisioned: true, toPackage: null);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        var provider = new FakeProvisioningProvider();

        var outcome = await CompleteOnAsync(provider, requestId);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PRODUCT_PACKAGE_MISSING);
        provider.ChangePackageCalls.Should().Be(0);
    }
}
