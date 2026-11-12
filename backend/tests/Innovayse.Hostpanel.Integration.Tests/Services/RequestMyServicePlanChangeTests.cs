using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RequestMyServicePlanChange;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>POST /api/me/services/{id}/plan-change</c>: an upgrade raises its proration invoice and moves the
/// service when it is paid, a downgrade waits for staff or is credited at once as the setting says, every refusal names its
/// code, and a stranger is told the service does not exist.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RequestMyServicePlanChangeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the plan change of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(int serviceId) => $"/api/me/services/{serviceId}/plan-change";

    /// <summary>Waits until a condition on the service holds; the plan change is applied by an event handler after the payment's save.</summary>
    /// <param name="serviceId">The service to read.</param>
    /// <param name="holds">The condition.</param>
    /// <returns>The service as last read.</returns>
    private async Task<ClientService> WaitForServiceAsync(int serviceId, Func<ClientService, bool> holds)
    {
        var service = await ServiceChangeTestSeeds.ReadServiceAsync(factory, serviceId);
        for (var attempt = 0; attempt < 50 && !holds(service); attempt++)
        {
            await Task.Delay(200);
            service = await ServiceChangeTestSeeds.ReadServiceAsync(factory, serviceId);
        }

        return service;
    }

    /// <summary>Tells whether a pending change set is the save that links the proration invoice to its request.</summary>
    /// <param name="tracker">The change tracker of the save.</param>
    /// <returns><see langword="true"/> when a request is being saved with an invoice id.</returns>
    private static bool LinksTheInvoice(ChangeTracker tracker) =>
        tracker.Entries<ServiceChangeRequest>().Any(e => e.State == EntityState.Modified && e.Entity.InvoiceId is not null);

    /// <summary>
    /// Refuses the save that links the invoice to its request, and just before it removes the client's invoice on a connection of its
    /// own, as if another process had deleted it, so the clean-up asks Billing to cancel an invoice it cannot find, which Billing
    /// answers as a failure, not as an exception.
    /// </summary>
    /// <param name="tracker">The change tracker of the save.</param>
    /// <param name="clientId">The client whose invoice disappears.</param>
    /// <returns><see langword="true"/> for the linking save, which must fail.</returns>
    private static bool LinksAfterTheInvoiceIsGone(ChangeTracker tracker, int clientId)
    {
        if (!LinksTheInvoice(tracker))
        {
            return false;
        }

        using var connection = new NpgsqlConnection(tracker.Context.Database.GetConnectionString());
        connection.Open();
        using var delete = new NpgsqlCommand(
            """
            DELETE FROM "InvoiceItems" WHERE "InvoiceId" IN (SELECT "Id" FROM "Invoices" WHERE "ClientId" = @client);
            DELETE FROM "Invoices" WHERE "ClientId" = @client;
            """,
            connection);
        delete.Parameters.AddWithValue("client", clientId);
        delete.ExecuteNonQuery();
        return true;
    }

    /// <summary>
    /// Builds a host where the save that links the invoice to the request fails, as does every save that <paramref name="alsoRefuses"/>
    /// names, and whose handler logs into <paramref name="logger"/>.
    /// </summary>
    /// <param name="logger">The logger that stands in for the handler's own.</param>
    /// <param name="alsoRefuses">Tells whether a later save of the clean-up must fail too.</param>
    /// <returns>The isolated host; the caller disposes it.</returns>
    private WebApplicationFactory<Program> CrashingHost(
        CapturingLogger<RequestMyServicePlanChangeHandler> logger, Func<ChangeTracker, bool> alsoRefuses) =>
        FailingSaveHost
            .Create(factory, new ManualTimeProvider(DateTimeOffset.UtcNow), tracker => LinksTheInvoice(tracker) || alsoRefuses(tracker))
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<ILogger<RequestMyServicePlanChangeHandler>>(logger)));

    /// <summary>An upgrade is approved at once, raises the invoice of the prorated charge, and moves the service when that invoice is paid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_RaisesTheProrationInvoice_AndTheServiceMovesWhenItIsPaid()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var expected = ServiceChangeTestSeeds.ExpectedProration(10m, 20m, seeded.NextRenewalAt);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(nameof(ServiceChangeType.Upgrade));
        body.GetProperty("status").GetString().Should().Be(nameof(ServiceChangeStatus.Approved));
        body.GetProperty("prorationAmount").GetDecimal().Should().Be(expected);
        var invoiceId = body.GetProperty("invoiceId").GetInt32();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId);
            invoice.ClientId.Should().Be(clientId);
            invoice.Total.Should().Be(expected);
        }

        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);

        await ServiceChangeTestSeeds.PayInvoiceAsync(await ServicesTestSeeds.AdminClientAsync(factory), invoiceId, expected);

        var moved = await WaitForServiceAsync(seeded.ServiceId, s => s.ProductId == seeded.ToProductId);
        moved.ProductId.Should().Be(seeded.ToProductId);
        moved.RecurringAmount.Should().Be(20m);
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, body.GetProperty("requestId").GetInt32());
        request.Status.Should().Be(ServiceChangeStatus.Completed);
        request.InvoiceId.Should().Be(invoiceId);
    }

    /// <summary>
    /// Two upgrades requested for one service at the same moment leave exactly one request and one invoice: the loser is
    /// refused with <c>SERVICE_CHANGE_ALREADY_PENDING</c> and never raises an invoice a client could pay for a change that
    /// does not happen.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoSimultaneousUpgrades_LeaveOneRequestAndOneInvoice()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-race");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);

        var responses = await Task.WhenAll(
            owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId }),
            owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId }));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var requests = await context.ServiceChangeRequests.AsNoTracking().Where(r => r.ServiceId == seeded.ServiceId).ToListAsync();
        requests.Should().ContainSingle().Which.InvoiceId.Should().NotBeNull();
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == clientId)).Should().Be(1);
    }

    /// <summary>An upgrade requested when no time is left in the cycle has nothing to charge and is applied at once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_WithNothingLeftToProrate_IsAppliedAtOnceWithoutAnInvoice()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-zero");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m, renewalIn: TimeSpan.FromMinutes(-5));

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be(nameof(ServiceChangeStatus.Completed));
        body.GetProperty("prorationAmount").GetDecimal().Should().Be(0m);
        body.GetProperty("invoiceId").ValueKind.Should().Be(JsonValueKind.Null);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.ToProductId);
    }

    /// <summary>With the automatic approval off, a downgrade waits for staff: nothing is credited and the plan stays.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Downgrade_WithoutAutoApproval_WaitsForStaff()
    {
        await ServiceChangeTestSeeds.SetAutoApproveDowngradesAsync(factory, false);
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-down-wait");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(nameof(ServiceChangeType.Downgrade));
        body.GetProperty("status").GetString().Should().Be(nameof(ServiceChangeStatus.Pending));
        body.GetProperty("invoiceId").ValueKind.Should().Be(JsonValueKind.Null);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>With the automatic approval on, a downgrade is applied at once and the prorated amount lands on the balance.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Downgrade_WithAutoApproval_CreditsTheBalanceAtOnce()
    {
        await ServiceChangeTestSeeds.SetAutoApproveDowngradesAsync(factory, true);
        try
        {
            var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-down-auto");
            var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m);
            var expected = ServiceChangeTestSeeds.ExpectedProration(20m, 10m, seeded.NextRenewalAt);

            var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("status").GetString().Should().Be(nameof(ServiceChangeStatus.Completed));
            body.GetProperty("prorationAmount").GetDecimal().Should().Be(expected);
            var service = await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId);
            service.ProductId.Should().Be(seeded.ToProductId);
            service.RecurringAmount.Should().Be(10m);
            (await ServiceChangeTestSeeds.CreditOfAsync(factory, clientId)).Should().Be(expected);
            (await ServiceChangeTestSeeds.PlanChangeCreditLinesOfAsync(factory, clientId)).Should().Be(1);
        }
        finally
        {
            await ServiceChangeTestSeeds.SetAutoApproveDowngradesAsync(factory, false);
        }
    }

    /// <summary>
    /// When the hosting panel refuses the new package, the change is not applied and nothing is credited, and the request stays
    /// Approved for staff to retry.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Downgrade_WhenThePanelRefusesThePackage_Returns502AndKeepsTheRequestApproved()
    {
        await ServiceChangeTestSeeds.SetAutoApproveDowngradesAsync(factory, true);
        try
        {
            var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-down-panel");
            var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m, provisioned: true);
            var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, new FakeProvisioningProvider { ShouldFail = true });
            var http = host.CreateClient();
            http.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

            var response = await http.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

            response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
            (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED);
            (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
            (await ServiceChangeTestSeeds.CreditOfAsync(factory, clientId)).Should().Be(0m);
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var request = await context.ServiceChangeRequests.AsNoTracking().SingleAsync(r => r.ServiceId == seeded.ServiceId);
            request.Status.Should().Be(ServiceChangeStatus.Approved);
        }
        finally
        {
            await ServiceChangeTestSeeds.SetAutoApproveDowngradesAsync(factory, false);
        }
    }

    /// <summary>A second request while one is open is a conflict.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOpenRequestAlreadyExists_Returns409WithAlreadyPending()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-pending");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_ALREADY_PENDING);
    }

    /// <summary>The unique index on open requests refuses a second Pending or Approved request for one service, whatever the handler saw.</summary>
    /// <param name="secondStatus">The status of the second request: Pending or Approved, both open.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ServiceChangeStatus.Pending)]
    [InlineData(ServiceChangeStatus.Approved)]
    public async Task TheDatabase_RefusesASecondOpenRequestForOneService(ServiceChangeStatus secondStatus)
    {
        var (_, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, $"plan-index-{secondStatus}");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);

        var act = () => ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, secondStatus);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    /// <summary>A completed request is history: a new request for the same service is accepted by the index.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheDatabase_AcceptsAnOpenRequestAfterACompletedOne()
    {
        var (_, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-index-completed");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Completed);

        var act = () => ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A request that was rejected no longer blocks a new one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARejectedRequest_DoesNotBlockANewOne()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-after-reject");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 20m, 10m);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Rejected);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A plan that costs the same as the current one is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APlanAtTheSamePrice_Returns400WithSamePrice()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-same-price");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 10m);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_SAME_PRICE);
    }

    /// <summary>The plan the service already runs on is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheCurrentPlan_Returns400WithSameProduct()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-same-product");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.FromProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_SAME_PRODUCT);
    }

    /// <summary>A product of another group is not a plan of this service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AProductOfAnotherGroup_Returns400WithProductNotAvailable()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-other-group");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var elsewhere = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, 30m));

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = elsewhere });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_PRODUCT_NOT_AVAILABLE);
    }

    /// <summary>An inactive plan cannot be moved to.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnInactivePlan_Returns400WithProductNotAvailable()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-inactive");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.Products.FirstAsync(p => p.Id == seeded.ToProductId)).Deactivate();
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_PRODUCT_NOT_AVAILABLE);
    }

    /// <summary>A plan with no price for the service's cycle in the client's currency is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APlanWithoutAPriceForTheCycle_Returns400WithPriceUnavailable()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-no-price");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var target = await context.Products.Include(p => p.Prices).FirstAsync(p => p.Id == seeded.ToProductId);
            target.RemovePrices("USD");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_PRICE_UNAVAILABLE);
    }

    /// <summary>An id that names no product is a missing product.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownProduct_Returns404WithProductNotFound()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-unknown-product");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = 999_999_999 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A service that is not active cannot change plan.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APendingService_Returns400WithServiceNotActive()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-pending-service");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await owner.PostAsJsonAsync(RouteOf(serviceId), new { toProductId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
    }

    /// <summary>A service with no renewal date has no cycle to prorate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AServiceWithoutARenewalDate_Returns400WithNoRenewalDate()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-no-renewal");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var service = await context.ClientServices.FirstAsync(s => s.Id == seeded.ServiceId);
            service.ChangeBillingTerms(new ServiceBillingTerms(service.BillingCycle, 1, 10m, 10m, null, null, null, null));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_NO_RENEWAL_DATE);
    }

    /// <summary>A body with a non-positive plan id fails validation before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ANonPositiveProductId_Returns400()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-invalid");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);

        var response = await owner.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A stranger is told the service does not exist, as for an id that names nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AStrangersService_Returns404WithServiceNotFound()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-owner");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, ownerClientId, 10m, 20m);
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Without a session the route is refused before ownership is considered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutASession_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(RouteOf(ServicesTestSeeds.NoSuchServiceId), new { toProductId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// When the save that links the proration invoice to the request fails, the invoice is cancelled and the request taken
    /// back, so no payable invoice is left next to a request that is never applied and the service's slot is free again.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_WhenTheLinkingSaveFails_CancelsTheInvoiceAndRemovesTheRequest()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-crash");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        using var host = FailingSaveHost.Create(factory, new ManualTimeProvider(DateTimeOffset.UtcNow), LinksTheInvoice);
        using var crashing = host.CreateClient();
        crashing.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await crashing.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        // The simulated failure is an InvalidOperationException, which the exception middleware answers with 400: that the
        // caller still sees it proves the cleanup did not replace the original error.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ServiceChangeRequests.AsNoTracking().AnyAsync(r => r.ServiceId == seeded.ServiceId)).Should().BeFalse();
        var invoices = await context.Invoices.AsNoTracking().Where(i => i.ClientId == clientId).ToListAsync();
        invoices.Should().ContainSingle().Which.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>
    /// When the invoice cannot be withdrawn after the linking save failed, it is logged as critical for staff, the request is
    /// still taken back, and the caller still sees the original error, not the failure of the clean-up.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_WhenTheInvoiceCannotBeCancelled_LogsItAndKeepsTheOriginalError()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-nocancel");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var logger = new CapturingLogger<RequestMyServicePlanChangeHandler>();
        using var host = CrashingHost(
            logger, tracker => tracker.Entries<Invoice>().Any(e => e.State == EntityState.Modified && e.Entity.Status == InvoiceStatus.Cancelled));
        using var crashing = host.CreateClient();
        crashing.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await crashing.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Critical).Which.Message.Should().Contain("could not be cancelled");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ServiceChangeRequests.AsNoTracking().AnyAsync(r => r.ServiceId == seeded.ServiceId)).Should().BeFalse();
        var invoices = await context.Invoices.AsNoTracking().Where(i => i.ClientId == clientId).ToListAsync();
        invoices.Should().ContainSingle().Which.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// When Billing refuses to cancel the invoice with a failure instead of an exception (here the invoice is gone), it is logged as
    /// critical with the refusal's code, the request is still taken back, and the caller still sees the original error.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_WhenBillingRefusesToCancelTheInvoice_LogsTheCodeAndKeepsTheOriginalError()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-refused");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var logger = new CapturingLogger<RequestMyServicePlanChangeHandler>();

        using var host = FailingSaveHost
            .Create(factory, new ManualTimeProvider(DateTimeOffset.UtcNow), tracker => LinksAfterTheInvoiceIsGone(tracker, clientId))
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<ILogger<RequestMyServicePlanChangeHandler>>(logger)));
        using var crashing = host.CreateClient();
        crashing.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await crashing.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Critical).Which.Message
            .Should().Contain("could not be cancelled").And.Contain(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ServiceChangeRequests.AsNoTracking().AnyAsync(r => r.ServiceId == seeded.ServiceId)).Should().BeFalse();
    }

    /// <summary>
    /// When the request cannot be removed after the linking save failed, the invoice is still cancelled, the failure is logged as
    /// critical for staff, and the caller still sees the original error.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_WhenTheRequestCannotBeRemoved_LogsItAndKeepsTheOriginalError()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-noremove");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var logger = new CapturingLogger<RequestMyServicePlanChangeHandler>();
        using var host = CrashingHost(logger, tracker => tracker.Entries<ServiceChangeRequest>().Any(e => e.State == EntityState.Deleted));
        using var crashing = host.CreateClient();
        crashing.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await crashing.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Critical).Which.Message.Should().Contain("could not be removed");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ServiceChangeRequests.AsNoTracking().AnyAsync(r => r.ServiceId == seeded.ServiceId)).Should().BeTrue();
        var invoices = await context.Invoices.AsNoTracking().Where(i => i.ClientId == clientId).ToListAsync();
        invoices.Should().ContainSingle().Which.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>The proration invoice falls due after the configured payment term, not after a number fixed in the handler.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_InvoiceFallsDueAfterTheConfiguredPaymentTerm()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-up-term");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Orders:InvoicePaymentTermDays", "21"));
        using var configured = host.CreateClient();
        configured.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await configured.PostAsJsonAsync(RouteOf(seeded.ServiceId), new { toProductId = seeded.ToProductId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invoiceId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId);
        invoice.DueDate.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(21), TimeSpan.FromMinutes(10));
    }
}
