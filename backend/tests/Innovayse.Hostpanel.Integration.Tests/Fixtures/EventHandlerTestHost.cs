using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared plumbing for the per-handler event tests (<c>Domains</c>, <c>Notifications</c> and
/// <c>Support</c> event handlers): a host whose SMTP relay is a <see cref="FakeEmailSender"/>, and a
/// client backed by a genuine ASP.NET Identity user. Kept here once so each handler's test class
/// states only the event it publishes and the effect it expects.
/// </summary>
public static class EventHandlerTestHost
{
    /// <summary>
    /// Builds a host of its own, with the SMTP relay replaced by <paramref name="emailSender"/>, so
    /// the fake a test reads back is the one the real handler chain actually wrote to.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host to derive from.</param>
    /// <param name="emailSender">The fake relay the host's send-email pipeline will use.</param>
    /// <param name="adminEmail">The platform admin address to configure, or <c>null</c> to leave it unset.</param>
    /// <param name="contactEmail">The contact-form inbox to configure, or <c>null</c> to leave it unset.</param>
    /// <returns>The isolated host.</returns>
    public static WebApplicationFactory<Program> WithFakeRelay(
        IntegrationTestFactory factory, IEmailSender emailSender, string? adminEmail = null, string? contactEmail = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (adminEmail is not null)
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { [$"{NotificationOptions.SectionName}:AdminEmail"] = adminEmail }));
            }

            if (contactEmail is not null)
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { [$"{NotificationOptions.SectionName}:ContactEmail"] = contactEmail }));
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);

                // Seeding saves must not publish their own events; each test dispatches its one event by hand.
                services.RemoveAll<IDomainEventGate>();
                services.AddScoped<IDomainEventGate, SuppressedDomainEventGate>();
            });
        });

    /// <summary>
    /// Seeds a client backed by a genuine ASP.NET Identity user, and returns it alongside the
    /// user's email. <see cref="Client"/> itself carries no email field; the address lives on
    /// the identity account <c>IIdentityProvider.FindBySubjectAsync</c> resolves.
    /// </summary>
    /// <param name="services">The scoped service provider to resolve the user manager from.</param>
    /// <param name="context">The data context to seed the client into.</param>
    /// <param name="emailPrefix">The prefix of the generated, unique email address.</param>
    /// <param name="firstName">The client's first name.</param>
    /// <param name="lastName">The client's last name.</param>
    /// <returns>The saved client and the identity email it is backed by.</returns>
    public static async Task<SeededIdentityClient> SeedIdentityClientAsync(
        IServiceProvider services,
        IHostpanelDbContext context,
        string emailPrefix,
        string firstName = "Ada",
        string lastName = "Lovelace")
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        var user = new AppUser { UserName = email, Email = email, FirstName = firstName, LastName = lastName, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, "Pass@123!");
        Assert.True(created.Succeeded);

        var client = Client.Create(user.Id, firstName, lastName, email, TestMoments.Seeded);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return new SeededIdentityClient(client, email);
    }
}
