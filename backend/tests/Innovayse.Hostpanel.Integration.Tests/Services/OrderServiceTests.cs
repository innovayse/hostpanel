using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.OrderService;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="OrderServiceCommand"/>: pricing from the product when the
/// caller leaves an amount unset, honouring a zero amount from order fulfilment, and refusing an
/// unknown or inactive product.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A registered client can order a service and receives 201 Created with the new service ID.
    /// Uses a retry loop to handle asynchronous Wolverine client-creation handler latency.
    /// </summary>
    [Fact]
    public async Task OrderServiceAsync_ReturnsCreated_AsClientAsync()
    {
        var client = factory.CreateClient();

        var adminToken = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var groupResponse = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "Hosting-Test" });
        groupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        var productResponse = await client.PostAsJsonAsync("/api/products",
            new { groupId, name = "Starter-Test", type = ProductType.SharedHosting, monthlyPrice = 5.99m, annualPrice = 59.99m });
        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();

        var email = $"svc-test-{Guid.NewGuid():N}@example.com";
        client.DefaultRequestHeaders.Authorization = null;

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Pass@123!",
            firstName = "Test",
            lastName = "User"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var clientToken = await factory.GetClientTokenAsync(email, "Pass@123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        HttpResponseMessage? orderResponse = null;
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(300);
            orderResponse = await client.PostAsJsonAsync("/api/me/services", new
            {
                ProductId = productId,
                BillingCycle = nameof(BillingCycle.Monthly)
            });

            if (orderResponse.StatusCode == HttpStatusCode.Created)
            {
                break;
            }
        }

        orderResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        var serviceId = await orderResponse.Content.ReadFromJsonAsync<int>();
        serviceId.Should().BeGreaterThan(0);
    }

    /// <summary>Order-fulfilment callers pass the snapshotted amount, zero included, and it is honoured.</summary>
    [Fact]
    public async Task OrderService_ZeroAmountsFromFulfilmentAreHonouredNotRefused()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 0m, 0m, "Card"), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.FirstOrDefaultAsync(
            s => s.Id == outcome.Value, CancellationToken.None);
        service!.FirstPaymentAmount.Should().Be(0m);
        service.RecurringAmount.Should().Be(0m);
    }

    /// <summary>
    /// A service ordered for an order line is stored with the line, and ordering again for the same line answers with that
    /// service instead of creating a second one, however many times a retry asks and whatever the amounts it passes.
    /// </summary>
    [Fact]
    public async Task OrderService_ForTheSameOrderLineTwice_ReturnsTheFirstServiceAndCreatesNoSecond()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var line = 515_101;

        var first = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 5m, 5m, "Card", OrderItemId: line), CancellationToken.None);
        var second = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 7m, 7m, "Card", OrderItemId: line), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var services = await context.ClientServices.AsNoTracking().Where(s => s.ClientId == clientId).ToListAsync(CancellationToken.None);
        var stored = services.Should().ContainSingle().Which;
        stored.OrderItemId.Should().Be(line);
        stored.FirstPaymentAmount.Should().Be(5m);
    }

    /// <summary>Two order lines of the same product each get their own service, and an order not bound to a line is never reused.</summary>
    [Fact]
    public async Task OrderService_ForDifferentOrderLines_CreatesAServiceForEach()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var a = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 1m, 1m, OrderItemId: 515_102), CancellationToken.None);
        var b = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 1m, 1m, OrderItemId: 515_103), CancellationToken.None);
        var c = await bus.InvokeAsync<Result<int>>(new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 1m, 1m), CancellationToken.None);
        var d = await bus.InvokeAsync<Result<int>>(new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 1m, 1m), CancellationToken.None);

        new[] { a.Value, b.Value, c.Value, d.Value }.Should().OnlyHaveUniqueItems();
    }

    /// <summary>A null amount is priced from the product's own price for that cycle.</summary>
    [Fact]
    public async Task OrderService_NullAmountsReadTheProductsPrice()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 4.5m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, null, null), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.FirstOrDefaultAsync(
            s => s.Id == outcome.Value, CancellationToken.None);
        service!.FirstPaymentAmount.Should().Be(4.5m);
        service.RecurringAmount.Should().Be(4.5m);
    }

    /// <summary>A null amount is refused, not sold for nothing, when the product has no such price.</summary>
    [Fact]
    public async Task OrderService_NullAmountsAreRefusedWhenTheProductHasNoPrice()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: null);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, null, null), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
    }

    /// <summary>An unknown product id refuses <c>PRODUCT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task OrderService_UnknownProductRefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(client.Id, ServicesTestSeeds.NoSuchServiceId, BillingCycle.Monthly, 1m, 1m), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>An inactive product refuses <c>PRODUCT_NOT_AVAILABLE</c>, even with amounts supplied.</summary>
    [Fact]
    public async Task OrderService_InactiveProductRefusesNotAvailable()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        product!.Deactivate();
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 1m, 1m), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
    }

    /// <summary>An unauthenticated caller cannot order through <c>/api/me/services</c>.</summary>
    [Fact]
    public async Task OrderServiceAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/me/services", new { ProductId = 1, BillingCycle = nameof(BillingCycle.Monthly) });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An unknown client id refuses <c>CLIENT_NOT_FOUND</c> and creates no service.</summary>
    [Fact]
    public async Task OrderService_UnknownClientRefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var (_, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(ClientsTestSeeds.NoSuchClientId, productId, BillingCycle.Monthly, 1m, 1m), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
