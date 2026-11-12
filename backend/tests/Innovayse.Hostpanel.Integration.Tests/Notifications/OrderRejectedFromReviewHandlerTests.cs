using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Orders.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>OrderRejectedFromReviewHandler</c>: the client whose order staff refused is mailed the order number and the reason, in
/// every language the template is seeded in. Each test builds its own host with the SMTP relay replaced by <see cref="FakeEmailSender"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderRejectedFromReviewHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The refused order's client is mailed once, with the order number and the reason staff gave.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MailsTheClientTheOrderNumberAndTheReason()
    {
        var emailSender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, emailSender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var seeded = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "rejected");

        await scope.ServiceProvider.PublishAndWaitAsync(
            new OrderRejectedFromReviewEvent(77, seeded.Client.Id, "ORD-0077", "The card could not be verified.") { OccurredAt = DateTimeOffset.UtcNow });

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.To.Should().Be(seeded.Email);
        message.Subject.Should().Contain("ORD-0077");
        message.Body.Should().Contain("ORD-0077").And.Contain("The card could not be verified.").And.NotContain("{{");
    }

    /// <summary>The template is stored in English, Russian and Armenian once seeded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeedsTheTemplateInEveryLanguage()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await OrderRejectedTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        await OrderRejectedTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var languages = await context.EmailTemplates.AsNoTracking()
            .Where(t => t.Slug == OrderRejectedTemplatesSeeder.RejectedSlug)
            .Select(t => t.Language)
            .ToListAsync();
        languages.Should().BeEquivalentTo(["en", "ru", "hy"]);
    }
}
