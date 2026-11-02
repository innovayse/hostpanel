using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Services.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>ServiceTerminationFailedAdminHandler</c>: an automatic termination that could not
/// remove the hosting account mails the configured staff address, seeding the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceTerminationFailedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>Publishing the event sends one staff mail naming the service and the reason, and the template row exists.</summary>
    [Fact]
    public async Task ServiceTerminationFailed_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        const int serviceId = 424242;

        await bus.PublishAsync(new ServiceTerminationFailedEvent(serviceId, "SERVICE_TERMINATE_UPSTREAM_FAILED") { OccurredAt = DateTimeOffset.UtcNow });

        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == AdminEmail); i++)
        {
            await Task.Delay(200);
        }

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#424242");
        mail.Body.Should().Contain("SERVICE_TERMINATE_UPSTREAM_FAILED");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.ServiceTerminationFailedSlug))
            .Should().BeTrue();
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    [Fact]
    public async Task ServiceTerminationFailed_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceTerminationFailedEvent(1, "x") { OccurredAt = DateTimeOffset.UtcNow });

        sender.Sent.Should().BeEmpty();
    }
}
