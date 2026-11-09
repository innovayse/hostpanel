using Innovayse.Hostpanel.Application.Billing.Commands.SendRenewalReminder;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="SendRenewalReminderHandler"/> against Postgres: one invoice, one threshold, one mail,
/// and a recorded row that makes a repeat a no-op.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SendRenewalReminderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds an unpaid renewal invoice, raised eleven days ago and due in three, for a fresh client.</summary>
    /// <param name="services">The scoped service provider.</param>
    /// <returns>The client's email and the invoice id.</returns>
    private static async Task<RenewalReminderTarget> SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(services, context, "single-reminder");
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(client.Id, now.AddDays(3), "USD", now);
        invoice.AddServiceRenewal("Hosting renewal", 25m, 1, now.AddDays(3), now.AddDays(33));
        invoice.UpdateOptions(now.AddDays(-11), now.AddDays(3), null, 0m);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return new(email, invoice.Id);
    }

    /// <summary>A due invoice is mailed and its threshold recorded; invoking again is a Success with no second mail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueInvoice_SendsOnceAndRecords()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var (email, invoiceId) = await SeedAsync(scope.ServiceProvider);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var first = await bus.InvokeAsync<Result>(new SendRenewalReminderCommand(invoiceId), CancellationToken.None);
        var second = await bus.InvokeAsync<Result>(new SendRenewalReminderCommand(invoiceId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        sender.Sent.Where(m => m.To == email).Should().ContainSingle();
        (await context.InvoiceReminders.AsNoTracking().CountAsync(r => r.InvoiceId == invoiceId && r.DaysBefore == 3)).Should().Be(1);
    }

    /// <summary>
    /// When the threshold's row already exists (another run recorded it) the command succeeds and sends
    /// nothing. This covers the pre-check; the DbUpdateException branch for a race between the check
    /// and the save needs interleaved scopes and is not exercised here.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AlreadyRecorded_SucceedsWithoutMail()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var (email, invoiceId) = await SeedAsync(scope.ServiceProvider);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.InvoiceReminders.Add(InvoiceReminder.Create(invoiceId, 3, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new SendRenewalReminderCommand(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Sent.Where(m => m.To == email).Should().BeEmpty();
    }

    /// <summary>An unknown invoice is a quiet Success: nothing to remind.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownInvoice_IsSuccess()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new SendRenewalReminderCommand(int.MaxValue), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// A client who turned payment reminders off is not mailed, but the threshold is still spent so
    /// the reminder is not retried every day.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OverdueNoticesOff_SendsNothingButRecordsTheThreshold()
    {
        var sender = new FakeEmailSender();
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, sender).Services.CreateScope();
        var (email, invoiceId) = await SeedAsync(scope.ServiceProvider);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await context.Invoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.ClientId).FirstAsync();
        await ClientSettingsSeeds.SetAsync(factory, clientId, overdueNotices: false);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new SendRenewalReminderCommand(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Sent.Where(m => m.To == email).Should().BeEmpty();
        (await context.InvoiceReminders.AsNoTracking().CountAsync(r => r.InvoiceId == invoiceId && r.DaysBefore == 3)).Should().Be(1);
    }
}
