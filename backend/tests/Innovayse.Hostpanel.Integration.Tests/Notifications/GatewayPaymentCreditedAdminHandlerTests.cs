using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>GatewayPaymentCreditedAdminHandler</c>: a gateway payment its invoice could not
/// record mails the configured staff address, saying whether the amount went to the client's balance
/// or is held for an operator, and seeds the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GatewayPaymentCreditedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>A credited payment sends one staff mail naming the invoice, the reference and the amount, and the template row exists.</summary>
    [Fact]
    public async Task GatewayPaymentCredited_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(new GatewayPaymentCreditedEvent(616161, 7, "bank-gateway", "ref-credited-1", 4.5m, "USD", Credited: true)
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        await WaitForMailAsync(sender);

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#616161");
        mail.Body.Should().Contain("bank-gateway").And.Contain("ref-credited-1").And.Contain("4.5").And.Contain("USD");
        mail.Body.Should().Contain("added to the client").And.NotContain("held for an operator");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentCreditedSlug))
            .Should().BeTrue();
    }

    /// <summary>A payment held for an operator says so instead of claiming it was credited.</summary>
    [Fact]
    public async Task GatewayPaymentHeld_MailsStaffThatAnOperatorMustResolveIt()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(new GatewayPaymentCreditedEvent(626262, 7, "bank-gateway", "ref-held-1", 10m, "EUR", Credited: false)
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        await WaitForMailAsync(sender);

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#626262");
        mail.Body.Should().Contain("held for an operator").And.NotContain("added to the client");
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    [Fact]
    public async Task GatewayPaymentCredited_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(
            new GatewayPaymentCreditedEvent(1, 1, "bank-gateway", "ref-none", 1m, "USD", Credited: true) { OccurredAt = DateTimeOffset.UtcNow },
            CancellationToken.None);

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>Waits, up to ten seconds, for the staff mail to reach the fake relay.</summary>
    /// <param name="sender">The fake relay the host sends through.</param>
    /// <returns>A task that completes once the mail arrived or the wait ran out.</returns>
    private static async Task WaitForMailAsync(FakeEmailSender sender)
    {
        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == AdminEmail); i++)
        {
            await Task.Delay(200);
        }
    }
}
