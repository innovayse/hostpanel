using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for the admin <c>POST /api/admin/service-change-requests/{id}/reject</c> action: staff turn a request
/// down with a note, and an upgrade's unpaid proration invoice is cancelled with it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RejectServiceChangeRequestTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route that rejects a request.</summary>
    /// <param name="requestId">The request.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(int requestId) => $"/api/admin/service-change-requests/{requestId}/reject";

    /// <summary>Rejecting a pending downgrade keeps the note and leaves the plan and the balance alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APendingDowngrade_IsRejectedWithTheNote()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(requestId), new { note = "Not this month" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        request.Status.Should().Be(ServiceChangeStatus.Rejected);
        request.Note.Should().Be("Not this month");
        request.ResolvedAt.Should().NotBeNull();
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
        (await ServiceChangeTestSeeds.CreditOfAsync(factory, seeded.ClientId)).Should().Be(0m);
    }

    /// <summary>Rejecting an approved upgrade that still waits for payment cancels its unpaid invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnApprovedUpgrade_IsRejected_AndItsUnpaidInvoiceIsCancelled()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(RouteOf(requestId), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Rejected);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>An invoice cancelled by hand before the reject does not stop the reject, so a reject that failed halfway can be repeated.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnAlreadyCancelledInvoice_DoesNotStopTheReject()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/billing/{invoiceId}/cancel", null)).EnsureSuccessStatusCode();

        var response = await admin.PostAsJsonAsync(RouteOf(requestId), new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Rejected);
    }

    /// <summary>A completed plan change cannot be taken back, and a rejected one is not rejected twice.</summary>
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

    /// <summary>
    /// Cancelling the invoice also rejects the request through its event, and that rejection can be saved first. The staff reject
    /// is then still answered as a success, the request stays rejected, and the staff note is kept.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenTheCancelledInvoicesEventRejectsFirst_TheStaffRejectSucceedsAndKeepsItsNote()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);
        var race = new RejectRaceSaveInterceptor();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));
        using var staff = host.CreateClient();
        staff.DefaultRequestHeaders.Authorization = (await ServicesTestSeeds.AdminClientAsync(factory)).DefaultRequestHeaders.Authorization;

        var response = await staff.PostAsJsonAsync(RouteOf(requestId), new { note = "Staff reason" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        race.Fired.Should().BeTrue();
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        request.Status.Should().Be(ServiceChangeStatus.Rejected);
        request.Note.Should().Be("Staff reason");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Cancelled);
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
