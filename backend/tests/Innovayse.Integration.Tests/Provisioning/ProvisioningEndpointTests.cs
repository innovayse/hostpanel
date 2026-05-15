namespace Innovayse.Integration.Tests.Provisioning;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Innovayse.Domain.Products;

/// <summary>Integration tests for /api/provisioning endpoints.</summary>
public sealed class ProvisioningEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Creates a product group, product, client user, and a service order.
    /// Returns the service ID so provisioning tests have a valid target.
    /// </summary>
    /// <param name="http">Authenticated HTTP client to use for setup calls.</param>
    /// <param name="adminToken">Admin JWT token for admin-only setup operations.</param>
    /// <returns>The ID of the newly created client service.</returns>
    private async Task<int> SeedServiceAsync(HttpClient http, string adminToken)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var groupResponse = await http.PostAsJsonAsync("/api/products/groups",
            new { name = $"Prov-Group-{Guid.NewGuid():N}" });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        var productResponse = await http.PostAsJsonAsync("/api/products",
            new
            {
                groupId,
                name = $"Prov-Plan-{Guid.NewGuid():N}",
                type = ProductType.SharedHosting,
                monthlyPrice = 9.99m,
                annualPrice = 99.99m
            });
        productResponse.EnsureSuccessStatusCode();
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();

        // Register a client user
        var email = $"prov-client-{Guid.NewGuid():N}@example.com";
        http.DefaultRequestHeaders.Authorization = null;

        var registerResponse = await http.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Pass@123!",
            firstName = "Prov",
            lastName = "Client"
        });
        registerResponse.EnsureSuccessStatusCode();

        var clientToken = await factory.GetClientTokenAsync(email, "Pass@123!");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        // Retry ordering service until the async client creation handler completes
        HttpResponseMessage? orderResponse = null;
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(300);
            orderResponse = await http.PostAsJsonAsync("/api/me/services", new
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
        return await orderResponse.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Admin can provision a pending service — expects 200 OK.</summary>
    [Fact]
    public async Task Provision_AdminCanProvisionServiceAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();

        var serviceId = await SeedServiceAsync(http, adminToken);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Client role calling /api/provisioning/{id}/provision receives 403 Forbidden.</summary>
    [Fact]
    public async Task Provision_ClientForbiddenAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();

        var serviceId = await SeedServiceAsync(http, adminToken);

        // Register and authenticate as plain client
        var email = $"prov-forbidden-{Guid.NewGuid():N}@example.com";
        http.DefaultRequestHeaders.Authorization = null;

        var registerResponse = await http.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Pass@123!",
            firstName = "Forbidden",
            lastName = "Client"
        });
        registerResponse.EnsureSuccessStatusCode();

        var clientToken = await factory.GetClientTokenAsync(email, "Pass@123!");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Admin can suspend a provisioned service — expects 200 OK.</summary>
    [Fact]
    public async Task Suspend_AdminCanSuspendAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();

        var serviceId = await SeedServiceAsync(http, adminToken);

        // First provision the service
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var provisionResponse = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);
        provisionResponse.EnsureSuccessStatusCode();

        // Now suspend it
        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{serviceId}/suspend",
            new { reason = "Non-payment" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Admin can retrieve credentials for a provisioned service — expects 200 OK.</summary>
    [Fact]
    public async Task GetCredentials_AdminCanGetCredentialsAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();

        var serviceId = await SeedServiceAsync(http, adminToken);

        // Provision first so ProvisioningRef is set
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var provisionResponse = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);
        provisionResponse.EnsureSuccessStatusCode();

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/credentials");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
