using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>GatewayPaymentStalledAdminHandler</c>: a gateway payment that stays pending for an
/// unusual time mails the configured staff address, seeding the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GatewayPaymentStalledAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>Publishing the event sends one staff mail naming the invoice and the hours, and the template row exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GatewayPaymentStalled_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(new GatewayPaymentStalledEvent(515151, 7, "bank-gateway", 26) { OccurredAt = DateTimeOffset.UtcNow });

        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == AdminEmail); i++)
        {
            await Task.Delay(200);
        }

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#515151");
        mail.Body.Should().Contain("bank-gateway").And.Contain("26");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentStalledSlug))
            .Should().BeTrue();
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GatewayPaymentStalled_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new GatewayPaymentStalledEvent(1, 1, "bank-gateway", 24) { OccurredAt = DateTimeOffset.UtcNow });

        sender.Sent.Should().BeEmpty();
    }
}
