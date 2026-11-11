using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/departments/{id}</c>: an admin can update a support department, an id names no
/// department is refused <c>DEPARTMENT_NOT_FOUND</c>, and
/// the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDepartmentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an existing department.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateADepartmentAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateDepartmentAsync(client);

        var response = await client.PutAsJsonAsync($"/api/departments/{id}", new
        {
            name = $"Renamed-{Guid.NewGuid():N}",
            email = "renamed@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a department that does not exist answers 404 with <c>DEPARTMENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownDepartmentAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/departments/{SupportTestSeeds.NoSuchId}", new
        {
            name = "Anything",
            email = "anything@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DEPARTMENT_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a department.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/departments/{SupportTestSeeds.NoSuchId}", new
        {
            name = "Anything",
            email = "anything@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
