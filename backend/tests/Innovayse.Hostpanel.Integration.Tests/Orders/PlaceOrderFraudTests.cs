using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> with fraud detection switched on, against Postgres: an order that scores into the review band is placed and
/// held, one that hits the block list or the reject threshold is refused with nothing created, every decision is logged, and a
/// payment for a held order provisions nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PlaceOrderFraudTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Places a guest order from a fixed address.</summary>
    /// <param name="email">The guest's e-mail address.</param>
    /// <param name="ip">The address the checkout comes from.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> PlaceGuestOrderAsync(string email, string ip = "198.51.100.60")
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var host = FraudTestSeeds.WithCallerAt(factory, ip);

        return await host.CreateClient().PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email,
            password = "Pass@123!",
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = "Monthly" } },
        });
    }

    /// <summary>With fraud detection off the order is placed as Pending and nothing is logged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenSwitchedOff_TheOrderIsPendingAndNothingIsLogged()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, enabled: false);
        var email = $"off-{Guid.NewGuid():N}@mailinator.com";

        var response = await PlaceGuestOrderAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orderId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
        (await db.FraudLogEntries.AsNoTracking().AnyAsync(l => l.Email == email)).Should().BeFalse();
    }

    /// <summary>An order that scores into the review band is placed, stored under review with its score and reasons, and logged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AReviewScore_HoldsTheOrderAndLogsIt()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var email = $"hold-{Guid.NewGuid():N}@mailinator.com";

        var response = await PlaceGuestOrderAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var orderId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.UnderReview);
        order.FraudScore.Should().Be(60);
        order.FraudReasons.Should().Equal(FraudReasonCode.DisposableEmail);
        order.IpAddress.Should().Be("198.51.100.60");
        var logged = await db.FraudLogEntries.AsNoTracking().SingleAsync(l => l.OrderId == orderId);
        logged.Decision.Should().Be(FraudDecision.Review);
        logged.Kind.Should().Be(FraudCheckKind.Order);
        logged.Email.Should().Be(email);
        logged.ClientId.Should().Be(order.ClientId);
    }

    /// <summary>A held order is not fulfilled by its payment.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APaymentForAHeldOrder_FulfilsNothing()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var response = await PlaceGuestOrderAsync($"pay-{Guid.NewGuid():N}@mailinator.com");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("orderId").GetInt32();
        var invoiceId = body.GetProperty("invoiceId").GetInt32();
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();

        await bus.InvokeAsync(new Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder.FulfillInvoicedOrderCommand(invoiceId));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.UnderReview);
        (await db.ClientServices.AsNoTracking().AnyAsync(s => db.OrderItems.Any(i => i.Id == s.OrderItemId && i.OrderId == orderId))).Should().BeFalse();
    }

    /// <summary>A blocked e-mail domain is refused with 403, nothing is created and the refusal is logged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ABlockListHit_RefusesTheOrderAndCreatesNothing()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "EmailDomain", value = "fraudsters.example" })).EnsureSuccessStatusCode();
        var email = $"bad-{Guid.NewGuid():N}@fraudsters.example";

        var response = await PlaceGuestOrderAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_FRAUD_REJECTED);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logged = await db.FraudLogEntries.AsNoTracking().SingleAsync(l => l.Email == email);
        logged.Decision.Should().Be(FraudDecision.Rejected);
        logged.OrderId.Should().BeNull();
        logged.Reasons.Should().Contain(FraudReasonCode.BlockedEmailDomain);
    }

    /// <summary>A blocked address is refused whatever the e-mail address is.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ABlockedAddress_RefusesTheOrder()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Cidr", value = "192.0.2.0/24" })).EnsureSuccessStatusCode();

        var response = await PlaceGuestOrderAsync($"ok-{Guid.NewGuid():N}@example.com", ip: "192.0.2.9");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_FRAUD_REJECTED);
    }
}
