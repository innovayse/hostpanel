using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>ServiceChangeInvoicePaidHandler</c> on the shared host, events live: paying the proration invoice of an approved upgrade is
/// enough, on its own, to move the service to the new plan.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceChangeInvoicePaidHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Recording the full payment of the invoice an approved upgrade waits for applies the upgrade.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APaidProrationInvoice_AppliesTheApprovedUpgrade()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);

        await ServiceChangeTestSeeds.PayInvoiceAsync(await ServicesTestSeeds.AdminClientAsync(factory), invoiceId, 5m);

        // The event is published after the payment's save, on Wolverine's local queue: poll.
        var request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        for (var attempt = 0; attempt < 50 && request.Status != ServiceChangeStatus.Completed; attempt++)
        {
            await Task.Delay(200);
            request = await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId);
        }

        request.Status.Should().Be(ServiceChangeStatus.Completed);
        var service = await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId);
        service.ProductId.Should().Be(seeded.ToProductId);
        service.RecurringAmount.Should().Be(20m);
    }

    /// <summary>The payment of an invoice no plan change waits for changes nothing and does not fail the other handlers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APaymentNoPlanChangeWaitsFor_ChangesNothing()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var pendingId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved);
        var unrelatedInvoice = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);

        await factory.Services.PublishAndWaitAsync(new PaymentReceivedEvent(unrelatedInvoice, seeded.ClientId, 5m, "tx-1")
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, pendingId)).Status.Should().Be(ServiceChangeStatus.Approved);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
    }

    /// <summary>A payment that leaves the proration invoice part paid applies nothing: the request keeps waiting for the rest.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APartPayment_LeavesTheUpgradeApproved()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 10m, 20m);
        var invoiceId = await ServiceChangeTestSeeds.SeedInvoiceAsync(factory, seeded.ClientId, 5m);
        var requestId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Approved, invoiceId);

        await factory.Services.PublishAndWaitAsync(new PaymentReceivedEvent(invoiceId, seeded.ClientId, 2m, "tx-part")
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        (await ServiceChangeTestSeeds.ReadRequestAsync(factory, requestId)).Status.Should().Be(ServiceChangeStatus.Approved);
        (await ServiceChangeTestSeeds.ReadServiceAsync(factory, seeded.ServiceId)).ProductId.Should().Be(seeded.FromProductId);
    }
}
