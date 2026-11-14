using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and assertion helpers shared by every <c>Support/&lt;UseCase&gt;Tests.cs</c> class, so
/// each per-use-case file states only what makes it different rather than re-deriving an admin
/// token or a department id.
/// </summary>
public static class SupportTestSeeds
{
    /// <summary>Shared test password for every user this class registers.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>An id no seed or prior test in this fixture ever creates.</summary>
    public const int NoSuchId = 999_999_999;

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client with the Admin role.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Reads the problem+json body's <c>type</c> (error code) field.</summary>
    /// <param name="response">The refused response.</param>
    /// <returns>The error code.</returns>
    public static async Task<string> ReadCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("type").GetString()!;
    }

    /// <summary>Reads the problem+json body's <c>type</c> field and asserts it against the expected code.</summary>
    /// <param name="response">The refused response.</param>
    /// <param name="expected">The error code the response must carry.</param>
    /// <returns>A task that completes once the assertion has run.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        (await ReadCodeAsync(response)).Should().Be(expected);
    }

    /// <summary>
    /// Registers a new client user, waits for Wolverine to create the corresponding
    /// <c>Client</c> row, and returns an authenticated <see cref="HttpClient"/> alongside its
    /// numeric client id.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">A short, unique-per-test label folded into the registered email.</param>
    /// <returns>The authenticated client and its numeric client id.</returns>
    public static async Task<SignedInClient> RegisterClientAsync(
        IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Test",
            lastName = label,
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clientId = 0;
        for (var i = 0; i < 10 && clientId == 0; i++)
        {
            await Task.Delay(300);
            var meResponse = await httpClient.GetAsync("/api/clients/me");
            if (meResponse.IsSuccessStatusCode)
            {
                var json = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("id", out var idProp))
                {
                    clientId = idProp.GetInt32();
                }
            }
        }

        clientId.Should().BeGreaterThan(0, "Wolverine should have created the Client record");
        return new SignedInClient(httpClient, clientId, userId, email);
    }

    /// <summary>Creates a support department as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <returns>The new department's id.</returns>
    public static async Task<int> CreateDepartmentAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/departments", new
        {
            name = $"Dept-{Guid.NewGuid():N}",
            email = "dept@example.com",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>Seeds a support department straight through the data context and returns its id.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The saved department's id.</returns>
    public static async Task<int> SeedDepartmentAsync(IHostpanelDbContext context)
    {
        var department = Department.Create($"Dept-{Guid.NewGuid():N}", "dept@example.com");
        context.Departments.Add(department);
        await context.SaveChangesAsync(CancellationToken.None);
        return department.Id;
    }

    /// <summary>Creates a download category as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <returns>The new category's id.</returns>
    public static async Task<int> CreateDownloadCategoryAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/downloads/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>Creates a predefined reply category as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <returns>The new category's id.</returns>
    public static async Task<int> CreatePredefinedReplyCategoryAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/predefined-replies/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            parentCategoryId = (int?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>Creates a knowledge base category as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <returns>The new category's id.</returns>
    public static async Task<int> CreateKbCategoryAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/knowledgebase/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>Creates a department and a staff-filed ticket in it, as admin, and returns the ticket id.</summary>
    /// <param name="factory">The shared API + PostgreSQL host, used to seed the client when none is named.</param>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="clientId">The client the ticket is filed on behalf of, or <see langword="null"/> to seed one.</param>
    /// <returns>The new ticket's id.</returns>
    public static async Task<int> CreateAdminTicketAsync(IntegrationTestFactory factory, HttpClient admin, int? clientId = null)
    {
        var departmentId = await CreateDepartmentAsync(admin);
        var ownerId = clientId ?? (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId;

        var response = await admin.PostAsJsonAsync("/api/tickets", new
        {
            clientId = ownerId,
            subject = "Admin-created ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>
    /// Seeds a ticket straight through the data context, in the given status, with the given flag and opened at the
    /// given moment, so a test can set up the exact mix a list filter must tell apart without driving the endpoints.
    /// </summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The client the ticket belongs to.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="departmentId">The department the ticket is filed under.</param>
    /// <param name="priority">The priority label.</param>
    /// <param name="openedAt">The moment the ticket was opened; it decides the list order.</param>
    /// <param name="status">The status the ticket ends in: open, awaiting a reply or closed.</param>
    /// <param name="flagged">Whether staff flagged the ticket.</param>
    /// <returns>The saved ticket's id.</returns>
    public static async Task<int> SeedTicketAsync(
        IHostpanelDbContext context,
        int clientId,
        string subject,
        int departmentId,
        TicketPriority priority,
        DateTimeOffset openedAt,
        TicketStatus status = TicketStatus.Open,
        bool flagged = false)
    {
        var ticket = Ticket.Create(clientId, subject, "Seeded for the list tests.", departmentId, priority, openedAt);
        if (status == TicketStatus.AwaitingReply)
        {
            ticket.AddReply("Staff answer.", "Staff", isStaffReply: true, openedAt);
        }

        if (status == TicketStatus.Closed)
        {
            ticket.Close(openedAt);
        }

        if (flagged)
        {
            ticket.ToggleFlag();
        }

        ticket.Backdate(openedAt, TimeSpan.FromSeconds(1));
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);
        return ticket.Id;
    }

    /// <summary>Seeds four tickets of one client that differ in status, priority, department and flag.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client the tickets belong to.</param>
    /// <returns>The mix, with the subjects alpha, beta, gamma and delta opened a minute apart in that order.</returns>
    public static async Task<TicketListMix> SeedTicketMixAsync(IntegrationTestFactory factory, int clientId)
    {
        var token = Guid.NewGuid().ToString("N")[..10];
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var departmentOne = await SeedDepartmentAsync(context);
        var departmentTwo = await SeedDepartmentAsync(context);
        var alpha = await SeedTicketAsync(
            context, clientId, $"alpha-{token}", departmentOne, TicketPriority.Low, TestMoments.Seeded.AddMinutes(1));
        var beta = await SeedTicketAsync(
            context, clientId, $"beta-{token}", departmentOne, TicketPriority.High, TestMoments.Seeded.AddMinutes(2), TicketStatus.Closed);
        var gamma = await SeedTicketAsync(
            context, clientId, $"gamma-{token}", departmentTwo, TicketPriority.High, TestMoments.Seeded.AddMinutes(3), flagged: true);
        var delta = await SeedTicketAsync(
            context, clientId, $"delta-{token}", departmentTwo, TicketPriority.Medium, TestMoments.Seeded.AddMinutes(4), TicketStatus.AwaitingReply);
        return new TicketListMix(token, clientId, departmentOne, departmentTwo, alpha, beta, gamma, delta);
    }

    /// <summary>Reads one page of a ticket list.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="url">The list address with its query string.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    public static async Task<(List<int> Ids, int TotalCount)> ListTicketIdsAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetInt32()).ToList();
        return (ids, body.RootElement.GetProperty("totalCount").GetInt32());
    }
}
