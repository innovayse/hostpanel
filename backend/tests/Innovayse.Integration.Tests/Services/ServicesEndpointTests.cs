namespace Innovayse.Integration.Tests.Services;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Innovayse.Domain.Products;

/// <summary>Integration tests for /api/services and /api/me/services endpoints.</summary>
public sealed class ServicesEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET /api/services without auth returns 401 Unauthorized.</summary>
    [Fact]
    public async Task GetServicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>GET /api/me/services without auth returns 401 Unauthorized.</summary>
    [Fact]
    public async Task GetMyServicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Admin calling GET /api/me/services returns 403 — endpoint requires Client role.</summary>
    [Fact]
    public async Task GetMyServicesAsync_Returns403_AsAdminAsync()
    {
        var client = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Admin GET /api/services returns 200 OK with paged results.</summary>
    [Fact]
    public async Task GetServicesAsync_Returns200_AsAdminAsync()
    {
        var client = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A registered client can order a service and receives 201 Created with the new service ID.
    /// Uses a retry loop to handle asynchronous Wolverine client-creation handler latency.
    /// </summary>
    [Fact]
    public async Task OrderServiceAsync_ReturnsCreated_AsClientAsync()
    {
        var client = factory.CreateClient();

        // 1. Create product group and product as admin
        var adminToken = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var groupResponse = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "Hosting-Test" });
        groupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        var productResponse = await client.PostAsJsonAsync("/api/products",
            new { groupId, name = "Starter-Test", type = ProductType.SharedHosting, monthlyPrice = 5.99m, annualPrice = 59.99m });
        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();

        // 2. Register a unique client account
        var email = $"svc-test-{Guid.NewGuid():N}@example.com";
        client.DefaultRequestHeaders.Authorization = null;

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Pass@123!",
            firstName = "Test",
            lastName = "User"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Authenticate as the new client
        var clientToken = await factory.GetClientTokenAsync(email, "Pass@123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        // 4. Wait for Wolverine async client-creation handler to complete, then order
        HttpResponseMessage? orderResponse = null;
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(300);
            orderResponse = await client.PostAsJsonAsync("/api/me/services", new
            {
                ProductId = productId,
                BillingCycle = "monthly"
            });

            if (orderResponse.StatusCode == HttpStatusCode.Created)
            {
                break;
            }
        }

        orderResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        var serviceId = await orderResponse.Content.ReadFromJsonAsync<int>();
        serviceId.Should().BeGreaterThan(0);
    }
}
