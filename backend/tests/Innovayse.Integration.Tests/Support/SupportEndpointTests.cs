namespace Innovayse.Integration.Tests.Support;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

/// <summary>Integration tests for the Support module endpoints.</summary>
public sealed class SupportEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Shared test password for seeded test users.</summary>
    private const string TestPassword = "Pass@123!";

    // ─── Test 1: Client can create a ticket ──────────────────────────────────

    /// <summary>
    /// A client POSTs to /api/me/tickets — expects 201 Created.
    /// </summary>
    [Fact]
    public async Task CreateTicket_ClientCanCreateAsync()
    {
        var httpClient = factory.CreateClient();

        // Register a new user and wait for the Client record
        var email = $"ticket-creator-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Ticket",
            lastName = "Creator"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientToken = authJson.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        // Wait for Wolverine to create the Client record
        var clientCreated = false;
        for (var i = 0; i < 10 && !clientCreated; i++)
        {
            await Task.Delay(300);
            var meResponse = await httpClient.GetAsync("/api/me");
            clientCreated = meResponse.IsSuccessStatusCode;
        }

        clientCreated.Should().BeTrue("Wolverine should have created the Client record");

        // Create a department first (via admin)
        var adminToken = await factory.GetAdminTokenAsync();
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var deptResponse = await adminClient.PostAsJsonAsync("/api/departments", new
        {
            name = $"Support-{Guid.NewGuid():N}",
            email = "support@example.com"
        });
        var deptBody = await deptResponse.Content.ReadAsStringAsync();
        if (!deptResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"POST /api/departments returned {(int)deptResponse.StatusCode}: {deptBody}");
        }

        var departmentId = int.Parse(deptBody.Trim());

        // Create ticket
        var response = await httpClient.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Test Ticket",
            message = "This is a test ticket body.",
            departmentId,
            priority = "Medium"
        });

        var ticketBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"POST /api/me/tickets returned {(int)response.StatusCode}: {ticketBody}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ─── Test 2: Client cannot access admin ticket list ───────────────────────

    /// <summary>
    /// A Client tries to GET /api/tickets — expects 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task ListTickets_ClientForbiddenAsync()
    {
        var httpClient = factory.CreateClient();

        var email = $"ticket-client-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Ticket",
            lastName = "Client"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientToken = authJson.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await httpClient.GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Test 3: Client can list their own tickets ────────────────────────────

    /// <summary>
    /// A registered client GETs /api/me/tickets — expects 200 OK.
    /// </summary>
    [Fact]
    public async Task GetMyTickets_ReturnsClientTicketsAsync()
    {
        var httpClient = factory.CreateClient();

        var email = $"my-tickets-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "My",
            lastName = "Tickets"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientToken = authJson.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        // Wait for Client record
        var clientCreated = false;
        for (var i = 0; i < 10 && !clientCreated; i++)
        {
            await Task.Delay(300);
            var meResponse = await httpClient.GetAsync("/api/me");
            clientCreated = meResponse.IsSuccessStatusCode;
        }

        clientCreated.Should().BeTrue("Wolverine should have created the Client record");

        var response = await httpClient.GetAsync("/api/me/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/me/tickets returned: {await response.Content.ReadAsStringAsync()}");
    }

    // ─── Test 4: KB public endpoint requires no auth ─────────────────────────

    /// <summary>
    /// An unauthenticated request to GET /api/knowledgebase — expects 200 OK.
    /// </summary>
    [Fact]
    public async Task Knowledgebase_PublicEndpoint_NoAuthAsync()
    {
        var httpClient = factory.CreateClient();
        // No Authorization header — anonymous request

        var response = await httpClient.GetAsync("/api/knowledgebase");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/knowledgebase returned: {await response.Content.ReadAsStringAsync()}");
    }
}
