using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared seeding and authentication helpers for the Notifications module's per-use-case
/// Integration tests: an admin bearer token, a freshly registered client, writing an
/// <see cref="EmailLog"/> row straight to the database, and reading a refusal's error code off a
/// problem+json body. Every <c>Notifications/*Tests.cs</c> file calls into this rather than
/// repeating its own copy.
/// </summary>
internal static class NotificationsTestSeeds
{
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

    /// <summary>Asserts the refusal carries the expected error code, which is what a client branches on.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }

    /// <summary>The body for creating a new template with a unique slug, so parallel tests never collide.</summary>
    /// <param name="slug">The unique slug to create.</param>
    /// <returns>The request body.</returns>
    public static object CreateTemplateBody(string slug) => new
    {
        slug,
        language = "en",
        subject = "Test subject {{name}}",
        body = "<p>Test body</p>",
        description = "Created by Notifications Integration tests",
    };

    /// <summary>Registers and signs in a new client, retrying until the async client-creation handler has run.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated portal client, plus the client's own primary key, identity subject and sign-in address.</returns>
    public static async Task<SignedInClient> RegisterClientAsync(
        IntegrationTestFactory factory)
    {
        var http = factory.CreateClient();
        var email = $"email-logs-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!";

        var registered = await http.PostAsJsonAsync(
            "/api/auth/register", new { email, password, firstName = "Log", lastName = "Reader" });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        var token = await factory.GetClientTokenAsync(email, password);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await http.GetAsync("/api/clients/me");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
                return new SignedInClient(http, profile.GetProperty("id").GetInt32(), userId, email);
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("CreateClientOnRegisterHandler did not run in time.");
    }

    /// <summary>Writes an email log row straight to the database, addressed to the given recipient.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="to">The recipient address the row is addressed to.</param>
    /// <param name="subject">The subject the row carries.</param>
    /// <param name="failure">Why delivery failed; <see langword="null"/> records a successful send.</param>
    /// <returns>The written row's primary key.</returns>
    public static async Task<int> SeedLogAsync(
        IntegrationTestFactory factory, string to, string subject, EmailSendFailure? failure = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = EmailLog.Create(to, subject, "<p>Body</p>", failure, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        db.EmailLogs.Add(log);
        await db.SaveChangesAsync();
        return log.Id;
    }

    /// <summary>Ensures an email template with the given slug exists, adding it only if it is missing.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="subject">The subject to seed it with, if it does not already exist.</param>
    /// <param name="isActive">Whether the seeded template, if it has to be created, starts active.</param>
    /// <returns>A task that completes once the template is known to exist.</returns>
    public static async Task EnsureTemplateAsync(
        IntegrationTestFactory factory, string slug, string subject, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == slug && t.Language == "en");
        if (exists)
        {
            return;
        }

        var template = EmailTemplate.Create(slug, "en", subject, "Body {{name}}", null);
        if (!isActive)
        {
            template.Deactivate();
        }

        db.EmailTemplates.Add(template);
        await db.SaveChangesAsync();
    }

    /// <summary>The platform admin address the notification handler tests configure and assert against.</summary>
    public const string AdminEmail = "admin@example.test";

    /// <summary>
    /// Ensures an email template with the given slug exists, adding it only if it is missing.
    /// </summary>
    /// <remarks>
    /// Idempotent because the two "domain-registration-failed" handlers share one event and both
    /// need the same two template rows; whichever test seeds second must not collide with the unique
    /// index on <see cref="EmailTemplate.Slug"/> and <see cref="EmailTemplate.Language"/> the first already satisfied.
    /// </remarks>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="subject">The subject to seed it with, if it does not already exist.</param>
    /// <returns>A task that completes once the template is known to exist.</returns>
    public static async Task EnsureTemplateAsync(IHostpanelDbContext context, string slug, string subject)
    {
        var exists = await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == slug && t.Language == "en", CancellationToken.None);
        if (exists)
        {
            return;
        }

        context.EmailTemplates.Add(EmailTemplate.Create(slug, "en", subject, "Body", null));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Seeds a hosting service for the given client, so a handler that loads the service by id
    /// before it loads the client finds a real row rather than returning early on a made-up id.
    /// </summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The owning client's primary key.</param>
    /// <returns>The new service's primary key.</returns>
    public static async Task<int> SeedClientServiceAsync(IHostpanelDbContext context, int clientId)
    {
        var group = ProductGroup.Create($"notification-events-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        return service.Id;
    }
}
