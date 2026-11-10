using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for <c>GET /api/me/services/{id}/cpanel-sso</c>: the caller may obtain an
/// control-panel sign-in link for their own provisioned service, a stranger's is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServiceControlPanelSsoUrlTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The owner can fetch their own service's control-panel sign-in link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_TheCallersOwnService_Returns200()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var (serviceId, ownerToken) = await ProvisioningTestSeeds.SeedOwnedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await http.GetAsync($"/api/me/services/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("url").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>A stranger's service is refused, not told which part is wrong.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_AStrangersService_Returns404WithServiceNotFound()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var ownerServiceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{ownerServiceId}/provision", null)).EnsureSuccessStatusCode();

        var strangerEmail = $"sso-stranger-{Guid.NewGuid():N}@example.com";
        var registerResponse = await http.PostAsJsonAsync("/api/auth/register", new
        {
            email = strangerEmail,
            password = "Pass@123!",
            firstName = "Stranger",
            lastName = "User"
        });
        registerResponse.EnsureSuccessStatusCode();
        var strangerToken = await factory.GetClientTokenAsync(strangerEmail, "Pass@123!");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);

        var response = await http.GetAsync($"/api/me/services/{ownerServiceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Requesting the control-panel sign-in link without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.GetAsync($"/api/me/services/{ProvisioningTestSeeds.NoSuchServiceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetSsoUrl_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "getmyservicecontrolpanelssourl-validation");

        var response = await client.GetAsync("/api/me/services/0/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>An account that does not allow single sign-on is refused with 403 and no link is issued.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_ByAccountWithSsoDisabled_Returns403WithClientSsoDisabled()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var (serviceId, ownerToken) = await ProvisioningTestSeeds.SeedOwnedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await context.ClientServices.Where(x => x.Id == serviceId).Select(x => x.ClientId).FirstAsync();
            await ClientSettingsSeeds.SetAsync(factory, clientId, allowSso: false);
        }

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await http.GetAsync($"/api/me/services/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_SSO_DISABLED);
    }
}
