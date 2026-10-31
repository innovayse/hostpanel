using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Provisioning a product with no server group when the servers run more than one supported panel.
/// A class of its own, so the second panel's server lives in this class's database only and never
/// changes what the other provisioning tests select.
/// </summary>
/// <param name="factory">The API + PostgreSQL host this class owns.</param>
public sealed class ProvisionServiceWithoutServerGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// With servers on two supported panels the package's panel is ambiguous, so the order is
    /// refused with 409 <c>PRODUCT_SERVER_GROUP_REQUIRED</c> instead of going to whichever is emptier.
    /// </summary>
    [Fact]
    public async Task Provision_ServersOnTwoPanels_Returns409WithServerGroupRequired()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsJsonAsync(
            "/api/admin/servers",
            ServersTestSeeds.ServerBody(
                $"Plesk-Server-{Guid.NewGuid():N}",
                hostname: "plesk.example.test",
                module: ServerModule.Plesk,
                accessHash: "plesk-key"))).EnsureSuccessStatusCode();

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PRODUCT_SERVER_GROUP_REQUIRED);
    }
}
