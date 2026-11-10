using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>PUT /api/clients/{id}</c> with a status: every status the form offers is applied through the
/// aggregate's transition matrix, Inactive included, and a move the matrix refuses answers with its own code.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateClientStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads the client's stored status.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The status.</returns>
    private async Task<ClientStatus> StatusOfAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Status).FirstAsync();
    }

    /// <summary>An active client can be marked Inactive, and Active again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_CanDeactivateAndReactivateAClientAsync()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var deactivated = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "Inactive"));

        deactivated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(clientId)).Should().Be(ClientStatus.Inactive);

        var activated = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "Active"));

        activated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(clientId)).Should().Be(ClientStatus.Active);
    }

    /// <summary>A suspended client cannot be moved straight to Inactive: the refusal carries its own code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesDeactivatingASuspendedClientAsync()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "Suspended")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "Inactive"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_DEACTIVATE_NOT_ALLOWED);
        (await StatusOfAsync(clientId)).Should().Be(ClientStatus.Suspended);
    }

    /// <summary>A closed client cannot be reactivated: the refusal is the activate code, the account stays closed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesReactivatingAClosedClientAsync()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "Closed")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "Active"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_ACTIVATE_NOT_ALLOWED);
        (await StatusOfAsync(clientId)).Should().Be(ClientStatus.Closed);
    }
}
