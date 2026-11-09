using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>RenewalPaymentReceivedHandler</c> on the shared host, events live: recording the full payment
/// of a renewal invoice through the admin route is enough, on its own, to extend the service: the
/// fast path from <c>PaymentReceivedEvent</c> to <c>ApplyPaidRenewalsCommand</c>, with no sweep run.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RenewalPaymentReceivedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Paying a renewal invoice in full extends the renewed service without the daily sweep.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FullPayment_ExtendsTheServiceThroughTheEvent()
    {
        int serviceId;
        int invoiceId;
        decimal total;
        DateTimeOffset periodEnd;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
            context.Clients.Add(client);
            var group = ProductGroup.Create($"renewal-fast-path-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);

            var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, DateTimeOffset.UtcNow);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);

            var start = DateTimeOffset.UtcNow.AddDays(5);
            var service = ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
            await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);
            service.ChangeBillingTerms(new ServiceBillingTerms(
                BillingCycle: BillingCycle.Monthly, Quantity: 1, FirstPaymentAmount: 5m,
                RecurringAmount: 5m, NextRenewalAt: start, PaymentMethod: null,
                PromotionCode: null, SubscriptionId: null));
            await context.SaveChangesAsync(CancellationToken.None);

            var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddServiceRenewal("Starter renewal", 5m, service.Id, start, start.AddMonths(1));
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);

            serviceId = service.Id;
            invoiceId = invoice.Id;
            total = invoice.Total;

            // As Postgres stored it (microsecond precision), so the comparison below is exact.
            periodEnd = (await context.Invoices.AsNoTracking().Include(i => i.Items)
                .FirstAsync(i => i.Id == invoice.Id)).Items.Single().RenewalPeriodEnd!.Value;
        }

        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = total,
            fees = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The event is published after the payment's save, on Wolverine's local queue: poll.
        DateTimeOffset? nextRenewalAt = null;
        for (var i = 0; i < 50 && nextRenewalAt != periodEnd; i++)
        {
            await Task.Delay(200);
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            nextRenewalAt = await context.ClientServices.AsNoTracking()
                .Where(s => s.Id == serviceId).Select(s => s.NextRenewalAt).FirstAsync();
        }

        nextRenewalAt.Should().Be(periodEnd);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var line = (await context.Invoices.AsNoTracking().Include(i => i.Items).FirstAsync(i => i.Id == invoiceId)).Items.Single();
            line.RenewalAppliedAt.Should().NotBeNull();
        }
    }
}
