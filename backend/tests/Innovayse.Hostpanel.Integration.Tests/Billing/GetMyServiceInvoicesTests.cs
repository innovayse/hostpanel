using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/services/{id}/invoices</c> against Postgres: the read is scoped to the calling
/// client through <c>IServiceOwnership</c>, which answers a <c>Result</c> refusal shared with the
/// Provisioning use cases that use the same port.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServiceInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creates a Pending service directly against the database, owned by the given client.</summary>
    /// <param name="clientId">The owning client's primary key.</param>
    /// <returns>The new service's primary key.</returns>
    private async Task<int> SeedPendingServiceAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var group = ProductGroup.Create($"my-invoices-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 2m, 20m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        return service.Id;
    }

    /// <summary>Reading a stranger's service invoices is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetInvoices_AStrangersService_Returns404WithServiceNotFound()
    {
        var (_, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-service-invoices");
        var serviceId = await SeedPendingServiceAsync(ownerClientId);
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "stranger-service-invoices");

        var response = await stranger.GetAsync($"/api/me/services/{serviceId}/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// The owner reads their own service's invoices: an empty list, with the unattributable count
    /// alongside it rather than a bare list a caller could mistake for "nothing was charged".
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetInvoices_TheCallersOwnService_Returns200WithEmptyList()
    {
        var (owner, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-service-empty");
        var serviceId = await SeedPendingServiceAsync(ownerClientId);

        var response = await owner.GetAsync($"/api/me/services/{serviceId}/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("invoices").GetArrayLength().Should().Be(0);
        body.GetProperty("unattributedInvoiceCount").GetInt32().Should().Be(0);
    }

    /// <summary>Reading a service's invoices is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetInvoicesRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/services/1/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetInvoices_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "getmyserviceinvoices-validation");

        var response = await client.GetAsync("/api/me/services/0/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
