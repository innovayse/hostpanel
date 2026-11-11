using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CorrectServiceTerminationDate;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>PUT /api/services/{id}/termination-date</c> and <see cref="CorrectServiceTerminationDateHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CorrectServiceTerminationDateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The moment a termination really happened, used by every test here.</summary>
    private static readonly DateTimeOffset EndedAt = new(2030, 1, 15, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Correcting the date of a terminated service answers 204 and stores the date.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CorrectTerminationDate_OnATerminatedService_StoresTheDateAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Terminated);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/termination-date", new { terminatedAt = EndedAt });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.TerminatedAt.Should().Be(EndedAt);
    }

    /// <summary>A service that is not terminated has no date to correct: 409 <c>SERVICE_NOT_TERMINATED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CorrectTerminationDate_OnAnActiveService_Returns409NotTerminatedAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/termination-date", new { terminatedAt = EndedAt });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_TERMINATED);
    }

    /// <summary>Correcting the date of a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CorrectTerminationDate_OnAnUnknownService_Returns404Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/termination-date", new { terminatedAt = EndedAt });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Correcting the date without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CorrectTerminationDate_WithoutAuth_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/termination-date", new { terminatedAt = EndedAt });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
