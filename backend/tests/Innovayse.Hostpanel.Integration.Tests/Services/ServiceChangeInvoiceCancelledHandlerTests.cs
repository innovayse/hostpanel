using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Services.Events;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>ServiceChangeInvoiceCancelledHandler</c> on the shared host, events live: cancelling the proration invoice of an approved
/// upgrade is enough, on its own, to reject the request and free the service for a new one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceChangeInvoiceCancelledHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Waits until the request leaves Approved; the rejection is made by an event handler after the cancellation's save, on its
    /// own queue, so the test polls for up to 10 seconds (50 reads, 200 ms apart) instead of assuming when it ran.
    /// </summary>
    /// <param name="requestId">The request to read.</param>
    /// <returns>The request as last read.</returns>
    private async Task<ServiceChangeRequest> WaitForRejectionAsync(int requestId)
    {
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        for (var attempt = 0; attempt < 50 && request.Status == ServiceChangeStatus.Approved; attempt++)
        {
            await Task.Delay(200);
            request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        }

        return request;
    }

    /// <summary>Cancelling the invoice an approved upgrade waits for rejects the request.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACancelledProrationInvoice_RejectsTheApprovedUpgrade()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);

        var cancelled = await (await ServicesTestSeeds.AdminClientAsync(factory)).PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        cancelled.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = await WaitForRejectionAsync(requestId);
        request.Status.Should().Be(ServiceChangeStatus.Rejected);
        request.ResolvedAt.Should().NotBeNull();
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
    }

    /// <summary>
    /// A client whose upgrade invoice was cancelled is not stuck: the service holds no open request any more, so the client can
    /// ask for the change again and gets a new invoice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AfterTheInvoiceIsCancelled_TheClientCanRequestTheChangeAgain()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plan-cancelled-invoice");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var route = $"/api/me/services/{seeded.ServiceId}/plan-change";
        var first = await owner.PostAsJsonAsync(route, new { toProductId = seeded.ToProductId });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();

        var cancelled = await (await ServicesTestSeeds.AdminClientAsync(factory))
            .PostAsync($"/api/billing/{body.GetProperty("invoiceId").GetInt32()}/cancel", content: null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForRejectionAsync(body.GetProperty("requestId").GetInt32())).Status.Should().Be(ServiceChangeStatus.Rejected);

        var second = await owner.PostAsJsonAsync(route, new { toProductId = seeded.ToProductId });

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var again = await second.Content.ReadFromJsonAsync<JsonElement>();
        again.GetProperty("status").GetString().Should().Be(nameof(ServiceChangeStatus.Approved));
        again.GetProperty("invoiceId").GetInt32().Should().NotBe(body.GetProperty("invoiceId").GetInt32());
    }

    /// <summary>The cancellation of an invoice no plan change waits for leaves the request that waits for another invoice Approved.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACancellationNoPlanChangeWaitsFor_ChangesNothing()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var waitingInvoice = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, waitingInvoice);
        var unrelatedInvoice = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);

        await factory.Services.PublishAndWaitAsync(new InvoiceCancelledEvent(unrelatedInvoice, seeded.ClientId)
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Approved);
    }

    /// <summary>A request that was already completed is history: the cancellation of its old invoice leaves it alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACompletedRequest_IsNotRejectedByTheCancellationOfItsInvoice()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Completed, invoiceId);

        var logger = new CapturingLogger<ServiceChangeInvoiceCancelledHandler>();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILogger<ServiceChangeInvoiceCancelledHandler>>(logger)));

        await host.Services.PublishAndWaitAsync(new InvoiceCancelledEvent(invoiceId, seeded.ClientId)
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        // Rejecting a Completed request would be refused and leave the status alone, so the status proves nothing: the handler
        // logs every refusal of the reject, and it logs nothing only when it never tried.
        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Completed);
        logger.Entries.Should().BeEmpty();
    }
}
