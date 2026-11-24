using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// The three free-trial events mail the client: one mail of the event's template to the client's address with the service link,
/// and nothing for a client without an address. Each test publishes the event once through the real bus on a host whose SMTP
/// relay is <see cref="FakeEmailSender"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TrialEmailHandlersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The moment every event is stamped with.</summary>
    private static readonly DateTimeOffset EndsAt = new(2026, 12, 5, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Publishes an event for a seeded trial and returns what was mailed.</summary>
    /// <param name="build">Builds the event from the service and client ids.</param>
    /// <returns>The recipient address and the mails sent.</returns>
    private async Task<(string Email, int ServiceId, FakeEmailSender Sender)> PublishAsync(Func<int, int, object> build)
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, NotificationsTestSeeds.AdminEmail);
        var (clientId, serviceId, email) = await TrialTestSeeds.SeedTrialAsync(host.Services, new TrialSeed());
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(build(serviceId, clientId));

        return (email, serviceId, sender);
    }

    /// <summary>The ending event mails the reminder with the days left and the service link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TrialEndingSoon_MailsTheReminder()
    {
        var (email, serviceId, sender) = await PublishAsync((s, c) => new TrialEndingSoonEvent(s, c, 3, EndsAt, TestMoments.Seeded));

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(email, mail.To);
        mail.Body.Should().Contain("Trial Starter").And.Contain("2026-12-05").And.Contain($"/client/services/{serviceId}");
        mail.Subject.Should().Contain("3");
    }

    /// <summary>The converted event mails the payment due date and the service link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TrialConverted_MailsThePaymentDueDate()
    {
        var (email, serviceId, sender) = await PublishAsync(
            (s, c) => new TrialConvertedEvent(s, c, EndsAt, EndsAt.AddDays(3), TestMoments.Seeded));

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(email, mail.To);
        mail.Body.Should().Contain("Trial Starter").And.Contain("2026-12-08").And.Contain($"/client/services/{serviceId}");
    }

    /// <summary>The ended event mails that the trial is over, with the service link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TrialEnded_MailsThatTheTrialEnded()
    {
        var (email, serviceId, sender) = await PublishAsync((s, c) => new TrialEndedEvent(s, c, EndsAt, TestMoments.Seeded));

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(email, mail.To);
        mail.Body.Should().Contain("Trial Starter").And.Contain("2026-12-05").And.Contain($"/client/services/{serviceId}");
    }

    /// <summary>A real client whose brief carries no address gets nothing, for each of the three events.</summary>
    /// <param name="kind">Which event to publish.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("ending")]
    [InlineData("converted")]
    [InlineData("ended")]
    public async Task AClientWithAnEmptyAddress_GetsNothing(string kind)
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, NotificationsTestSeeds.AdminEmail);
        var (clientId, serviceId, email) = await TrialTestSeeds.SeedTrialAsync(host.Services, new TrialSeed());
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        // A client whose sign-in account is gone resolves to a brief without an address.
        Assert.True((await users.DeleteAsync(user!)).Succeeded);

        await scope.ServiceProvider.PublishAndWaitAsync(Build(kind, serviceId, clientId));

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>Publishing the same event twice sends one mail.</summary>
    /// <param name="kind">Which event to publish.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("ending")]
    [InlineData("converted")]
    [InlineData("ended")]
    public async Task TheSameEventTwice_MailsOnce(string kind)
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, NotificationsTestSeeds.AdminEmail);
        var (clientId, serviceId, _) = await TrialTestSeeds.SeedTrialAsync(host.Services, new TrialSeed());
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(Build(kind, serviceId, clientId));
        await scope.ServiceProvider.PublishAndWaitAsync(Build(kind, serviceId, clientId));

        Assert.Single(sender.Sent);
    }

    /// <summary>Builds the event of a kind.</summary>
    /// <param name="kind">Which event.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <param name="clientId">The owning client.</param>
    /// <returns>The event.</returns>
    private static object Build(string kind, int serviceId, int clientId) => kind switch
    {
        "ending" => new TrialEndingSoonEvent(serviceId, clientId, 3, EndsAt, TestMoments.Seeded),
        "converted" => new TrialConvertedEvent(serviceId, clientId, EndsAt, EndsAt.AddDays(3), TestMoments.Seeded),
        _ => new TrialEndedEvent(serviceId, clientId, EndsAt, TestMoments.Seeded),
    };
}
