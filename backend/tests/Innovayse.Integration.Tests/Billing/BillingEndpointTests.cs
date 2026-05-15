namespace Innovayse.Integration.Tests.Billing;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

/// <summary>Integration tests for /api/billing and /api/me/billing endpoints.</summary>
public sealed class BillingEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Shared test password for all seeded test users.</summary>
    private const string TestPassword = "Pass@123!";
    [Fact]
    public async Task GetInvoicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/billing");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyInvoicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/billing");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyInvoicesAsync_Returns403_AsAdminAsync()
    {
        var client = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/me/billing");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetInvoicesAsync_Returns200_AsAdminAsync()
    {
        var client = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/billing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateInvoiceAsync_Returns201_AsAdminAsync()
    {
        var httpClient = factory.CreateClient();

        // 1. Register a new client user
        var email = $"billing-test-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Billing",
            lastName = "Client"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Use the client's own token + /api/me to retrieve the client ID (Wolverine async handler)
        var clientToken = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        int clientId = 0;
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(500);
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

        clientId.Should().BeGreaterThan(0, "client should be created by Wolverine handler");

        // 3. Switch to admin to create the invoice
        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 4. Create invoice
        var dueDate = DateTimeOffset.UtcNow.AddDays(30);
        var createResponse = await httpClient.PostAsJsonAsync("/api/billing", new
        {
            clientId,
            dueDate,
            items = new[]
            {
                new { description = "Web Hosting", unitPrice = 9.99m, quantity = 1 }
            }
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoiceId = await createResponse.Content.ReadFromJsonAsync<int>();
        invoiceId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMyInvoicesAsync_Returns200_AsClientAsync()
    {
        var client = factory.CreateClient();

        // 1. Register a new client user — token comes back directly from register response
        var email = $"billing-test-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Billing",
            lastName = "Client"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientToken = authJson.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        // 2. Retry until Wolverine client-creation handler completes — /api/me/billing returns 200
        HttpResponseMessage response = await client.GetAsync("/api/me/billing");
        for (var i = 0; i < 10 && response.StatusCode != HttpStatusCode.OK; i++)
        {
            await Task.Delay(500);
            response = await client.GetAsync("/api/me/billing");
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyInvoiceAsync_Returns403_ForOtherClientInvoiceAsync()
    {
        var httpClient = factory.CreateClient();

        // 1. Register client A — use token from register response directly
        var emailA = $"billing-test-{Guid.NewGuid():N}@example.com";
        var registerAResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = emailA,
            password = TestPassword,
            firstName = "Client",
            lastName = "Alpha"
        });
        var authJsonA = await registerAResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientAToken = authJsonA.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientAToken);

        // 2. Wait for client A's profile via /api/me

        int clientAId = 0;
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(500);
            var meResponse = await httpClient.GetAsync("/api/me");
            if (meResponse.IsSuccessStatusCode)
            {
                var json = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("id", out var idProp))
                {
                    clientAId = idProp.GetInt32();
                    break;
                }
            }
        }

        clientAId.Should().BeGreaterThan(0);

        // 3. Create invoice as admin
        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var dueDate = DateTimeOffset.UtcNow.AddDays(30);
        var createResponse = await httpClient.PostAsJsonAsync("/api/billing", new
        {
            clientId = clientAId,
            dueDate,
            items = new[]
            {
                new { description = "Web Hosting", unitPrice = 9.99m, quantity = 1 }
            }
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoiceId = await createResponse.Content.ReadFromJsonAsync<int>();

        // 4. Register client B and use their register-response token directly
        var emailB = $"billing-test-{Guid.NewGuid():N}@example.com";
        httpClient.DefaultRequestHeaders.Authorization = null;
        var registerBResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = emailB,
            password = TestPassword,
            firstName = "Client",
            lastName = "Beta"
        });
        var authJsonB = await registerBResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientBToken = authJsonB.GetProperty("accessToken").GetString()!;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientBToken);

        // Wait until client B's profile is created by Wolverine before hitting the billing endpoint
        var warmup = await httpClient.GetAsync("/api/me/billing");
        for (var i = 0; i < 10 && warmup.StatusCode != HttpStatusCode.OK; i++)
        {
            await Task.Delay(500);
            warmup = await httpClient.GetAsync("/api/me/billing");
        }

        var response = await httpClient.GetAsync($"/api/me/billing/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
