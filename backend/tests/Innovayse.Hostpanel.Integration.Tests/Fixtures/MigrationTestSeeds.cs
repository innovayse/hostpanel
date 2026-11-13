using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and assertion helpers shared by every migration use-case test class, so each one does
/// not repeat the admin client, job creation or refusal-code assertion logic (A7 ruling 1).
/// </summary>
public static class MigrationTestSeeds
{
    /// <summary>A source address nothing on this host listens on, so a connect attempt fails fast.</summary>
    public const string UnreachableSourceUrl = "http://127.0.0.1:1/modules/addons/innovayse_migration/api.php";

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Creates a migration job over HTTP and returns its id.</summary>
    /// <param name="client">The authenticated admin client to create it with.</param>
    /// <param name="sourceUrl">The source address to store on the job.</param>
    /// <returns>The new job's id.</returns>
    public static async Task<int> CreateJobAsync(HttpClient client, string sourceUrl)
    {
        var response = await client.PostAsJsonAsync("/api/admin/migrations", new
        {
            label = $"job-{Guid.NewGuid():N}",
            sourceUrl,
            selection = new
            {
                clients = false,
                invoices = false,
                services = false,
                domains = false,
                tickets = false,
                products = false,
                orders = false,
                transactions = false,
                quotes = false,
                knowledgebase = false,
                contacts = false,
                ticketReplies = false,
                announcements = false,
                downloads = false,
                networkIssues = false,
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Asserts the refusal carries the expected error code, which is what a client branches on.
    /// </summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }

    /// <summary>
    /// Builds a host of its own, with the real <see cref="IMigrationSource"/> replaced by
    /// <paramref name="source"/>, so a pull driven against it never makes a real HTTP call.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host to branch the isolated host from.</param>
    /// <param name="source">The fake source the isolated host's importers will page through.</param>
    /// <returns>The isolated host.</returns>
    public static WebApplicationFactory<Program> HostWithFakeSource(IntegrationTestFactory factory, IMigrationSource source) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IMigrationSource>(source)));

    /// <summary>
    /// Seeds a client backed by a genuine ASP.NET Identity user: <c>ResolveClientIdAsync</c>
    /// looks the client up by the identity subject <see cref="IIdentityProvider.FindByEmailAsync"/>
    /// resolves.
    /// </summary>
    /// <param name="services">The scoped service provider to resolve the user manager from.</param>
    /// <param name="context">The data context to seed the client into.</param>
    /// <returns>The saved client's email, the address every seeded record is filed under.</returns>
    public static async Task<string> SeedClientAsync(IServiceProvider services, IHostpanelDbContext context)
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var email = $"migration-{Guid.NewGuid():N}@example.test";
        var user = new AppUser { UserName = email, Email = email, FirstName = "Jane", LastName = "Doe", EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, "Pass@123!");
        Assert.True(created.Succeeded);

        context.Clients.Add(Client.Create(user.Id, "Jane", "Doe", email, TestMoments.Seeded));
        await context.SaveChangesAsync(CancellationToken.None);
        return email;
    }

    /// <summary>
    /// Resolves the identity subject behind <paramref name="email"/>. A client row stores only that
    /// subject (<see cref="Client.UserId"/>), never the address, so tests find a client through it.
    /// </summary>
    /// <param name="services">The scoped service provider to resolve the identity provider from.</param>
    /// <param name="email">The address the account was provisioned under.</param>
    /// <returns>The account's subject, or an empty string when no account carries that address.</returns>
    public static async Task<string> UserIdForEmailAsync(IServiceProvider services, string email)
    {
        var identity = services.GetRequiredService<IIdentityProvider>();
        var account = await identity.FindByEmailAsync(email, CancellationToken.None);
        return account?.Subject ?? string.Empty;
    }

    /// <summary>
    /// Creates a queued job that selects exactly one entity type for export, the shape every
    /// single-importer test needs, so the worker it drives runs only the importer under test.
    /// Queued, because the worker pulls only a job whose pull was requested.
    /// </summary>
    /// <param name="context">The data context to seed the job into.</param>
    /// <param name="only">The one entity type the job selects.</param>
    /// <returns>The saved job.</returns>
    public static async Task<MigrationJob> SeedJobSelectingOnlyAsync(
        IHostpanelDbContext context, Domain.Migration.Enums.MigrationEntityType only)
    {
        var job = MigrationJob.Create(new MigrationJobSetup(
            Label: "test",
            SourceUrl: "http://fake-source.invalid",
            Selection: new MigrationExportSelection(
                Clients: only == Domain.Migration.Enums.MigrationEntityType.Clients,
                Invoices: only == Domain.Migration.Enums.MigrationEntityType.Invoices,
                Services: only == Domain.Migration.Enums.MigrationEntityType.Services,
                Domains: only == Domain.Migration.Enums.MigrationEntityType.Domains,
                Tickets: only == Domain.Migration.Enums.MigrationEntityType.Tickets,
                Products: only == Domain.Migration.Enums.MigrationEntityType.Products,
                Orders: only == Domain.Migration.Enums.MigrationEntityType.Orders,
                Transactions: only == Domain.Migration.Enums.MigrationEntityType.Transactions,
                Quotes: only == Domain.Migration.Enums.MigrationEntityType.Quotes,
                Knowledgebase: only == Domain.Migration.Enums.MigrationEntityType.Knowledgebase,
                Contacts: only == Domain.Migration.Enums.MigrationEntityType.Contacts,
                TicketReplies: only == Domain.Migration.Enums.MigrationEntityType.TicketReplies,
                Announcements: only == Domain.Migration.Enums.MigrationEntityType.Announcements,
                Downloads: only == Domain.Migration.Enums.MigrationEntityType.Downloads,
                NetworkIssues: only == Domain.Migration.Enums.MigrationEntityType.NetworkIssues)));
        job.Queue(DateTimeOffset.UtcNow);
        context.MigrationJobs.Add(job);
        await context.SaveChangesAsync(CancellationToken.None);
        return job;
    }
}
