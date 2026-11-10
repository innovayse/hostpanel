using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Projects;

/// <summary>
/// Integration tests for the <c>GetProject</c> use case: <c>GET /api/admin/projects/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProjectTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The moment the host's clock stands at while the record is created.</summary>
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A stored project is returned with its id and every translation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ReturnsTheRecordWithTranslations()
    {
        using var host = new ClockedHost(factory, CreatedAt);
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(sortOrder: 4));

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{id}");

        fetched.GetProperty("id").GetInt32().Should().Be(id);
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(4);
        fetched.GetProperty("createdAt").GetDateTimeOffset().Should().Be(CreatedAt);
        fetched.GetProperty("translations").GetProperty("en").GetProperty("title").GetString().Should().Be("English title");
    }

    /// <summary>An id that names no project answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/projects/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PROJECT_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused as a validation failure before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_NonPositiveId_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/projects/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/projects/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
