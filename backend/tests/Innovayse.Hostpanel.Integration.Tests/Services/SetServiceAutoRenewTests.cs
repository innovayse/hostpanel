using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SetServiceAutoRenew;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>Integration tests for <c>PUT /api/services/{id}/auto-renew</c> and <see cref="SetServiceAutoRenewHandler"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetServiceAutoRenewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Switching off then on answers 204, stores the flag, leaves the other automation settings and logs both changes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_SwitchesTheFlagAndLogsIt()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var off = await admin.PutAsJsonAsync($"/api/services/{serviceId}/auto-renew", new { value = false });

        off.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AutoRenewOfAsync(serviceId)).Should().BeFalse();

        var on = await admin.PutAsJsonAsync($"/api/services/{serviceId}/auto-renew", new { value = true });

        on.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AutoRenewOfAsync(serviceId)).Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var lines = await context.ActivityLogs.AsNoTracking()
            .Where(l => l.Description.Contains($"Set Service Auto Renew - Service ID: {serviceId},"))
            .Select(l => l.Description)
            .ToListAsync();
        lines.Should().HaveCount(2);
        lines.Should().Contain(l => l.EndsWith("Auto Renew: False", StringComparison.Ordinal));
        lines.Should().Contain(l => l.EndsWith("Auto Renew: True", StringComparison.Ordinal));
    }

    /// <summary>The route id wins over a different id in the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_RouteIdWinsOverBodyId()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var other = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/services/{serviceId}/auto-renew", new { serviceId = other, value = false });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AutoRenewOfAsync(serviceId)).Should().BeFalse();
        (await AutoRenewOfAsync(other)).Should().BeTrue();
    }

    /// <summary>A service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_OnAnUnknownService_Returns404()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Without authentication the call is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_WithoutAuth_Returns401()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client who is not staff is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_AsAStranger_Returns403()
    {
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.PutAsJsonAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A zero id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_WithAZeroId_Returns400()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync("/api/services/0/auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Reads the stored flag of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The flag as stored.</returns>
    private async Task<bool> AutoRenewOfAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().Where(s => s.Id == serviceId).Select(s => s.AutoRenew).SingleAsync();
    }
}
