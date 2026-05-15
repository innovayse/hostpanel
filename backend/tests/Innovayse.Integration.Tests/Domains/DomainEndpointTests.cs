namespace Innovayse.Integration.Tests.Domains;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

/// <summary>Integration tests for /api/domains and /api/me/domains endpoints.</summary>
public sealed class DomainEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Shared test password for seeded test users.</summary>
    private const string TestPassword = "Pass@123!";

    // ─── Unauthenticated / authorization guard tests ──────────────────────────

    /// <summary>GET /api/domains without auth returns 401.</summary>
    [Fact]
    public async Task ListDomains_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/domains");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>GET /api/me/domains without auth returns 401.</summary>
    [Fact]
    public async Task GetMyDomains_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/domains");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── AdminOnly_ClientForbidden ────────────────────────────────────────────

    /// <summary>
    /// Authenticate as a regular Client and call the admin-only GET /api/domains endpoint.
    /// Expects 403 Forbidden because the endpoint requires Admin or Reseller role.
    /// </summary>
    [Fact]
    public async Task AdminOnly_ClientForbiddenAsync()
    {
        var httpClient = factory.CreateClient();

        // Register → get token directly from register response
        var email = $"domain-client-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Domain",
            lastName = "Client"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientToken = authJson.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await httpClient.GetAsync("/api/domains");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── CheckAvailability_ReturnsTrue ───────────────────────────────────────

    /// <summary>
    /// Admin calls GET /api/domains/check?name=available.com.
    /// StubRegistrarProvider always returns true, so response body should be true.
    /// </summary>
    [Fact]
    public async Task CheckAvailability_ReturnsTrueAsync()
    {
        var httpClient = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await httpClient.GetAsync("/api/domains/check?name=available.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("true");
    }

    // ─── Register_ValidRequest_Returns201 ────────────────────────────────────

    /// <summary>
    /// Admin POSTs to /api/domains/register with a valid body.
    /// Expects 201 Created and a Location header pointing to the new domain.
    /// </summary>
    [Fact]
    public async Task Register_ValidRequest_Returns201Async()
    {
        var httpClient = factory.CreateClient();

        // Need a real client ID — register a user and wait for Wolverine to create the Client record
        var clientId = await RegisterClientAndGetIdAsync(httpClient);

        // Switch to admin
        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var domainName = $"test-{Guid.NewGuid():N}";
        var response = await httpClient.PostAsJsonAsync("/api/domains/register", new
        {
            name = $"{domainName}.com",
            years = 1,
            clientId,
            registrant = new
            {
                firstName = "Test",
                lastName = "User",
                email = "registrant@example.com",
                phone = "+1.5555555555",
                address = "123 Main St",
                city = "Springfield",
                state = "IL",
                postalCode = "62701",
                country = "US"
            },
            enableWhoisPrivacy = false,
            autoRenew = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    // ─── GetById_AfterRegister_Returns200 ────────────────────────────────────

    /// <summary>
    /// Registers a domain as admin, then calls GET /api/domains/{id} and expects 200
    /// with a body that contains the registered domain name.
    /// </summary>
    [Fact]
    public async Task GetById_AfterRegister_Returns200Async()
    {
        var httpClient = factory.CreateClient();
        var clientId = await RegisterClientAndGetIdAsync(httpClient);

        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var uniqueLabel = Guid.NewGuid().ToString("N")[..12];
        var fqdn = $"{uniqueLabel}.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/domains/register", new
        {
            name = fqdn,
            years = 1,
            clientId,
            registrant = new
            {
                firstName = "Test",
                lastName = "User",
                email = "registrant@example.com",
                phone = "+1.5555555555",
                address = "123 Main St",
                city = "Springfield",
                state = "IL",
                postalCode = "62701",
                country = "US"
            },
            enableWhoisPrivacy = false,
            autoRenew = false
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Extract the domain ID from the Location header (/api/domains/{id})
        var location = registerResponse.Headers.Location!.ToString();
        var domainId = int.Parse(location.Split('/').Last());

        var getResponse = await httpClient.GetAsync($"/api/domains/{domainId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await getResponse.Content.ReadAsStringAsync();
        json.Should().Contain(uniqueLabel);
    }

    // ─── SetAutoRenew_Returns204 ──────────────────────────────────────────────

    /// <summary>
    /// Registers a domain, then calls PUT /api/domains/{id}/auto-renew with enabled=true.
    /// Expects 204 No Content.
    /// </summary>
    [Fact]
    public async Task SetAutoRenew_Returns204Async()
    {
        var httpClient = factory.CreateClient();
        var clientId = await RegisterClientAndGetIdAsync(httpClient);

        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var domainId = await RegisterDomainAsync(httpClient, clientId);

        var response = await httpClient.PutAsJsonAsync(
            $"/api/domains/{domainId}/auto-renew",
            new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── MyDomains_ClientCanSeeOwnDomains ─────────────────────────────────────

    /// <summary>
    /// Registers a user, waits for the Client record to be created by Wolverine,
    /// then calls GET /api/me/domains and expects 200 OK.
    /// </summary>
    [Fact]
    public async Task MyDomains_ClientCanSeeOwnDomainsAsync()
    {
        var httpClient = factory.CreateClient();

        var email = $"domain-owner-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Domain",
            lastName = "Owner"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientToken = authJson.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        // Wait for Wolverine to create the Client record — poll /api/me
        var clientCreated = false;
        for (var i = 0; i < 10 && !clientCreated; i++)
        {
            await Task.Delay(300);
            var meResponse = await httpClient.GetAsync("/api/me");
            clientCreated = meResponse.IsSuccessStatusCode;
        }

        clientCreated.Should().BeTrue("Wolverine should have created the Client record after registration");

        var response = await httpClient.GetAsync("/api/me/domains");
        var debugBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET /api/me/domains returned: {debugBody}");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new user, waits for the Wolverine handler to create the Client record,
    /// and returns the client's numeric ID from GET /api/me.
    /// </summary>
    /// <param name="httpClient">The shared <see cref="HttpClient"/> instance.</param>
    /// <returns>The numeric client ID.</returns>
    private async Task<int> RegisterClientAndGetIdAsync(HttpClient httpClient)
    {
        var email = $"domain-reg-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Domain",
            lastName = "Tester"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var clientToken = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        int clientId = 0;
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(300);
            var meResponse = await httpClient.GetAsync("/api/me");
            if (meResponse.IsSuccessStatusCode)
            {
                var json = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("id", out var idProp))
                {
                    clientId = idProp.GetInt32();
                    break;
                }
            }
        }

        clientId.Should().BeGreaterThan(0, "Wolverine should have created the Client record");
        return clientId;
    }

    /// <summary>
    /// Registers a domain for the given client (assumes the caller is already authenticated as Admin).
    /// </summary>
    /// <param name="httpClient">The shared <see cref="HttpClient"/> instance, authenticated as Admin.</param>
    /// <param name="clientId">The target client ID.</param>
    /// <returns>The new domain's primary key.</returns>
    private static async Task<int> RegisterDomainAsync(HttpClient httpClient, int clientId)
    {
        var label = Guid.NewGuid().ToString("N")[..12];
        var response = await httpClient.PostAsJsonAsync("/api/domains/register", new
        {
            name = $"{label}.com",
            years = 1,
            clientId,
            registrant = new
            {
                firstName = "Test",
                lastName = "User",
                email = "registrant@example.com",
                phone = "+1.5555555555",
                address = "123 Main St",
                city = "Springfield",
                state = "IL",
                postalCode = "62701",
                country = "US"
            },
            enableWhoisPrivacy = false,
            autoRenew = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = response.Headers.Location!.ToString();
        return int.Parse(location.Split('/').Last());
    }
}
