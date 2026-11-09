using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>POST /api/admin/migrations</c>, dispatching <c>CreateMigrationJobCommand</c>: creates a new
/// pending migration job and its shared secret key.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateMigrationJobTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a job answers 201 with a Location, and the job then shows up in the list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenListShowsTheNewJob()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/migrations", new
        {
            label = "acceptance",
            sourceUrl = "http://legacy.example.test",
            selection = new { clients = true },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetInt32();
        created.RootElement.GetProperty("sourceUrl").GetString().Should()
            .Be("http://legacy.example.test/modules/addons/innovayse_migration/api.php");

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/migrations");
        list!.Select(j => j.GetProperty("id").GetInt32()).Should().Contain(id);
    }

    /// <summary>An empty source address is refused before a job is written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnEmptySourceUrl()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/migrations", new { label = "x", sourceUrl = "", selection = new { clients = true } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body that names no entity selection is refused before a job is written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAMissingSelection()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/migrations", new { label = "x", sourceUrl = "http://legacy.example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/migrations", new { label = "x", sourceUrl = "http://legacy.example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
