using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>Helpers the fraud detection tests share: switching it on, hosts with a fixed caller address, and held orders.</summary>
public static class FraudTestSeeds
{
    /// <summary>Saves the fraud settings through the real admin endpoint.</summary>
    /// <param name="factory">The factory to mint an admin token from.</param>
    /// <param name="enabled">Whether fraud detection is on.</param>
    /// <param name="review">The review threshold.</param>
    /// <param name="reject">The reject threshold.</param>
    /// <param name="windowMinutes">The velocity window.</param>
    /// <param name="maxOrders">The orders-per-window limit.</param>
    /// <param name="maxAccounts">The accounts-per-address limit.</param>
    /// <returns>A task that completes once the settings are saved.</returns>
    public static async Task SaveSettingsAsync(
        IntegrationTestFactory factory,
        bool enabled = true,
        int review = 50,
        int reject = 90,
        int windowMinutes = 60,
        int maxOrders = 3,
        int maxAccounts = 3)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var response = await admin.PutAsJsonAsync("/api/admin/fraud/settings", new
        {
            enabled,
            manualReviewRiskScore = review,
            autoRejectRiskScore = reject,
            velocityWindowMinutes = windowMinutes,
            velocityMaxOrders = maxOrders,
            maxAccountsPerIp = maxAccounts,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Derives a host whose callers come from a fixed address and whose scoring service is the given fake.</summary>
    /// <param name="factory">The shared API + PostgreSQL host to derive from.</param>
    /// <param name="ipAddress">The address every request is resolved to.</param>
    /// <param name="provider">The scripted scoring service; the real one (not set up in tests) when omitted.</param>
    /// <returns>The derived host.</returns>
    public static WebApplicationFactory<Program> WithCallerAt(
        IntegrationTestFactory factory, string? ipAddress, FakeFraudScoringProvider? provider = null)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICurrentUserService>();
            services.AddSingleton<ICurrentUserService>(new GuestAddressCaller(ipAddress));

            if (provider is not null)
            {
                services.RemoveAll<IFraudScoringProvider>();
                services.AddSingleton<IFraudScoringProvider>(provider);
            }
        }));
    }

    /// <summary>Writes one decision to the fraud log through the real use case.</summary>
    /// <param name="factory">The factory whose database holds the log.</param>
    /// <param name="command">The decision.</param>
    /// <returns>The new log entry's id.</returns>
    public static async Task<int> RecordAsync(IntegrationTestFactory factory, RecordFraudDecisionCommand command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var recorded = await bus.InvokeAsync<Result<int>>(command, CancellationToken.None);
        recorded.IsSuccess.Should().BeTrue();
        return recorded.Value;
    }

    /// <summary>Seeds a pending order with its unpaid invoice and holds it for review.</summary>
    /// <param name="factory">The factory whose database the pair is seeded in.</param>
    /// <param name="paid">Whether the invoice was paid while the order waited.</param>
    /// <returns>The order id and its invoice id.</returns>
    public static async Task<SeededInvoicedOrder> SeedHeldOrderAsync(IntegrationTestFactory factory, bool paid = false)
    {
        var seeded = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == seeded.OrderId);
        order.HoldForReview(65, [FraudReasonCode.DisposableEmail, FraudReasonCode.OrderVelocity]);

        if (paid)
        {
            var invoice = await db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == seeded.InvoiceId);
            db.Transactions.Add(invoice.Settle("bank-gateway", $"paid-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, TransactionSource.Manual)!);
        }

        await db.SaveChangesAsync();
        return seeded;
    }
}
