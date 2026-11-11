using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for the admin <c>POST /api/admin/service-change-requests/{id}/approve</c> action: staff approve a pending
/// downgrade and it is applied, approving again retries a change the panel refused, and nothing that is closed or unpaid is applied.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApproveServiceChangeRequestTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route that approves a request.</summary>
    /// <param name="requestId">The request.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(int requestId) => $"/api/admin/service-change-requests/{requestId}/approve";

    /// <summary>Approving a pending downgrade applies it: the plan moves, the balance is credited and the note is kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APendingDowngrade_IsAppliedAndCredited()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(requestId), new { note = "Fine by us" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        request.Status.Should().Be(ServiceChangeStatus.Completed);
        request.Note.Should().Be("Fine by us");
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.ToProductId);
        var credited = ServiceChangeTestSeeds.ExpectedProration(20m, 10m, seeded.NextRenewalAt);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, seeded.ClientId)).Should().Be(credited);
    }

    /// <summary>
    /// A panel that refuses leaves the request Approved (answer 502, nothing credited); approving it again once the panel works
    /// applies it, and credits once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenThePanelRefuses_TheRequestStaysApproved_AndApprovingAgainRetriesIt()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m, provisioned: true);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);
        var failing = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var refused = await failing.PostAsJsonAsync(RouteOf(requestId), new { note = (string?)null });

        refused.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ServicesTestSeeds.CodeOfAsync(refused)).Should().Be(HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED);
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Approved);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, seeded.ClientId)).Should().Be(0m);

        var working = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider());
        var retried = await working.PostAsJsonAsync(RouteOf(requestId), new { note = (string?)null });

        retried.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Completed);
        (await ServiceChangeTestSeeds.PlanChangeCreditLinesOfAsync(factory, seeded.ClientId)).Should().Be(1);
    }

    /// <summary>An upgrade still waiting for its invoice to be paid is not applied by approving it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUpgradeWhoseInvoiceIsUnpaid_Returns402WithInvoiceUnpaid()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(requestId), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_INVOICE_UNPAID);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
    }

    /// <summary>A request that is already completed or rejected cannot be approved.</summary>
    /// <param name="status">A closed status.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ServiceChangeStatus.Completed)]
    [InlineData(ServiceChangeStatus.Rejected)]
    public async Task AClosedRequest_Returns409WithInvalidState(ServiceChangeStatus status)
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, status);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(requestId), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
    }

    /// <summary>An id that names no request answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownRequest_Returns404WithRequestNotFound()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(999_999_999), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CHANGE_REQUEST_NOT_FOUND);
    }

    /// <summary>A note over the limit fails validation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ANoteOverTheLimit_Returns400()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(1), new { note = new string('x', 2001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A client of the portal is not staff.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClient_Returns403()
    {
        var client = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await client.PostAsJsonAsync(RouteOf(1), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Without a session the route is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutASession_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(RouteOf(1), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
