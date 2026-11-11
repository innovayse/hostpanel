using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>POST /api/me/services/{id}/setup</c>: a stranger's service is
/// refused, before any provisioning is attempted, with the same answer a missing id gets.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetupMyServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Setting up a stranger's service is refused before any provisioning is attempted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_AStrangersService_Returns404WithServiceNotFound()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "setup-mine-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.PostAsJsonAsync($"/api/me/services/{serviceId}/setup", new
        {
            domain = "example.com",
            username = "jdoe",
            password = "Passw0rd!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Setting up the caller's own pending service, with no server registered, refuses, not crashes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_TheCallersOwnPendingService_WithNoServer_Returns503NoEligibleServer()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "setup-mine-own");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        var response = await owner.PostAsJsonAsync($"/api/me/services/{serviceId}/setup", new
        {
            domain = "example.com",
            username = "jdoe",
            password = "Passw0rd!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_ELIGIBLE_SERVER);
    }

    /// <summary>Setting up a service without authentication is refused before ownership is considered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/me/services/{ServicesTestSeeds.NoSuchServiceId}/setup", new
        {
            domain = "example.com",
            username = "jdoe",
            password = "Passw0rd!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A service with a non-free first payment that names no first invoice (created before services
    /// recorded it, or by an administrator) keeps the earlier behaviour: it passes the payment check
    /// and reaches provisioning, which refuses only because no server is registered for it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_AServiceWithoutFirstInvoice_PassesThePaymentCheck()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "setup-mine-noinv");
        var serviceId = await SeedOrderedServiceAsync(ownerClientId, null);

        var response = await PostSetupAsync(owner, serviceId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_ELIGIBLE_SERVER);
    }

    /// <summary>A service whose first invoice is still unpaid is refused with 402, before any provisioning.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_WithAnUnpaidFirstInvoice_Returns402FirstInvoiceUnpaid()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "setup-mine-unpaid");
        var serviceId = await SeedOrderedServiceAsync(ownerClientId, InvoiceStatus.Unpaid);

        var response = await PostSetupAsync(owner, serviceId);

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_FIRST_INVOICE_UNPAID);
    }

    /// <summary>
    /// A service whose first invoice is paid in full passes the payment check and reaches
    /// provisioning, which refuses only because no server is registered for it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_WithAPaidFirstInvoice_PassesThePaymentCheck()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "setup-mine-paid");
        var serviceId = await SeedOrderedServiceAsync(ownerClientId, InvoiceStatus.Paid);

        var response = await PostSetupAsync(owner, serviceId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_ELIGIBLE_SERVER);
    }

    /// <summary>Posts the setup wizard's hosting details for a service.</summary>
    /// <param name="client">The caller's authenticated client.</param>
    /// <param name="serviceId">The service to set up.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PostSetupAsync(HttpClient client, int serviceId) =>
        client.PostAsJsonAsync($"/api/me/services/{serviceId}/setup", new
        {
            domain = "example.com",
            username = "jdoe",
            password = "Passw0rd!"
        });

    /// <summary>
    /// Seeds a pending service ordered for a non-free first payment, with its first invoice in the
    /// given state.
    /// </summary>
    /// <param name="clientId">The owning client.</param>
    /// <param name="firstInvoiceStatus">
    /// <see cref="InvoiceStatus.Unpaid"/> or <see cref="InvoiceStatus.Paid"/> for the first invoice, or
    /// <see langword="null"/> when the service was ordered without one.
    /// </param>
    /// <returns>The service id.</returns>
    private async Task<int> SeedOrderedServiceAsync(int clientId, InvoiceStatus? firstInvoiceStatus)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;

        var group = ProductGroup.Create($"setup-paid-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, "Starter", null, null, null, "starter", ProductType.SharedHosting, 2m, 20m, now);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        int? invoiceId = null;
        if (firstInvoiceStatus is not null)
        {
            var invoice = Invoice.CreateDraft(clientId, now.AddDays(7), "USD", now);
            invoice.AddItem("Starter", 2m, 1);
            invoice.Publish();
            if (firstInvoiceStatus is InvoiceStatus.Paid)
            {
                invoice.RecordPaidHistory("first-payment", now);
            }

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var service = Domain.Services.ClientService.CreateOrdered(
            clientId, product.Id, BillingCycle.Monthly, null, 2m, 2m, null, invoiceId, now);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        return service.Id;
    }
}
