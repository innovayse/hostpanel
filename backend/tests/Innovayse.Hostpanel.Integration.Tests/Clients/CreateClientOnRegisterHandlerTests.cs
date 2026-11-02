using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.IntegrationEvents.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// Tests for <c>CreateClientOnRegisterHandler</c>: a registered account gets exactly one client row,
/// carrying the registration details from the event. The subject is created straight through the
/// identity store so no queued copy of the event races the one the test publishes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateClientOnRegisterHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The event creates a client row for the subject with the names and registration details it carries.</summary>
    [Fact]
    public async Task ClientRegistered_CreatesClientRowWithRegistrationDetailsAsync()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "on-register");
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(
            subject.UserId, subject.Email, "Grace", "Hopper",
            IpAddress: "203.0.113.7", UserAgent: "TestAgent/1.0",
            DeviceType: "Desktop", OperatingSystem: "Linux", Browser: "Firefox"));

        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(c => c.UserId == subject.UserId);
        client.FirstName.Should().Be("Grace");
        client.LastName.Should().Be("Hopper");
        client.RegistrationIp.Should().Be("203.0.113.7");
        client.RegistrationUserAgent.Should().Be("TestAgent/1.0");
        client.DeviceType.Should().Be("Desktop");
        client.OperatingSystem.Should().Be("Linux");
        client.Browser.Should().Be("Firefox");
    }

    /// <summary>A second delivery of the event for a subject that already has a client leaves the one row untouched.</summary>
    [Fact]
    public async Task ClientRegistered_Twice_KeepsSingleClientRowAsync()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "on-register-twice");
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(subject.UserId, subject.Email, "First", "Delivery"));
        await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(subject.UserId, subject.Email, "Second", "Delivery"));

        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clients = await db.Clients.AsNoTracking().Where(c => c.UserId == subject.UserId).ToListAsync();
        clients.Should().ContainSingle().Which.FirstName.Should().Be("First");
    }

    /// <summary>
    /// A delivery for an account that no longer exists (the demo reset truncated it after the event was
    /// queued) creates no client row pointing at nobody.
    /// </summary>
    [Fact]
    public async Task ClientRegistered_ForAnAccountThatNoLongerExists_CreatesNoClientAsync()
    {
        var userId = Guid.NewGuid().ToString();
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(userId, $"gone-{Guid.NewGuid():N}@example.test", "Gone", "Account"));

        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Clients.AsNoTracking().AnyAsync(c => c.UserId == userId)).Should().BeFalse();
    }
}
