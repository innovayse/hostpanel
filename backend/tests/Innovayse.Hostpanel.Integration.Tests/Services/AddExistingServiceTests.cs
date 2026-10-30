using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>POST /api/services/existing</c> and <see cref="AddExistingServiceHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddExistingServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The service is stored Active on the server, with the account, the amounts and a renewal one
    /// cycle after the start, and no password.
    /// </summary>
    [Fact]
    public async Task AddExisting_StoresAnActiveServiceOnTheServerAsync()
    {
        var ids = await SeedClientAndProductAsync();
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{Guid.NewGuid():N}.example.test", 22);
        var startedAt = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/services/existing", new
        {
            clientId = ids.ClientId,
            productId = ids.ProductId,
            billingCycle = nameof(BillingCycle.Annual),
            recurringAmount = 49.99m,
            serverId,
            domain = " site.example ",
            username = "siteuser",
            startedAt,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var serviceId = await response.Content.ReadFromJsonAsync<int>();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.ClientId.Should().Be(ids.ClientId);
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.ServerId.Should().Be(serverId);
        stored.Domain.Should().Be("site.example");
        stored.Username.Should().Be("siteuser");
        stored.ProvisioningRef.Should().Be("siteuser");
        stored.Password.Should().BeNull();
        stored.RecurringAmount.Should().Be(49.99m);
        stored.NextRenewalAt.Should().Be(startedAt.AddYears(1));
    }

    /// <summary>A second live service for the same username on the same server is refused with 409.</summary>
    [Fact]
    public async Task AddExisting_ForAnAccountAlreadyHeld_Returns409Async()
    {
        var ids = await SeedClientAndProductAsync();
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{Guid.NewGuid():N}.example.test", 22);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);
        var body = new
        {
            clientId = ids.ClientId,
            productId = ids.ProductId,
            billingCycle = nameof(BillingCycle.Monthly),
            recurringAmount = 5m,
            serverId,
            domain = "taken.example",
            username = "taken",
        };

        (await client.PostAsJsonAsync("/api/services/existing", body)).StatusCode.Should().Be(HttpStatusCode.Created);
        var response = await client.PostAsJsonAsync("/api/services/existing", body with { username = "TAKEN" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_HOSTING_ACCOUNT_EXISTS);
    }

    /// <summary>A server id that names no server is refused with 404 <c>SERVER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task AddExisting_OnAnUnknownServer_Returns404Async()
    {
        var ids = await SeedClientAndProductAsync();
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/services/existing", new
        {
            clientId = ids.ClientId,
            productId = ids.ProductId,
            billingCycle = nameof(BillingCycle.Monthly),
            recurringAmount = 5m,
            serverId = 999_999_999,
            domain = "site.example",
            username = "siteuser",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVER_NOT_FOUND);
    }

    /// <summary>A missing username is refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    [Fact]
    public async Task AddExisting_WithoutAUsername_Returns400Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/services/existing", new
        {
            clientId = 1,
            productId = 1,
            billingCycle = nameof(BillingCycle.Monthly),
            recurringAmount = 5m,
            serverId = 1,
            domain = "site.example",
            username = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Adding without authentication is refused.</summary>
    [Fact]
    public async Task AddExisting_WithoutAuth_Returns401Async()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/services/existing", new { clientId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Seeds a client and a product.</summary>
    /// <returns>Their ids.</returns>
    private async Task<ClientProductIds> SeedClientAndProductAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 5m);
    }
}
