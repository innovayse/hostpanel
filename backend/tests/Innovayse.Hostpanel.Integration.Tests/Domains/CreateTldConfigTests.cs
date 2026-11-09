using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>CreateTldConfig</c> use case:
/// <c>POST /api/admin/tld-configs</c>. The full CRUD lifecycle (read, list, update, delete) is
/// exercised here too, since a TLD configuration has no interesting existence except through
/// this create; the sibling use cases <see cref="GetTldConfigTests"/>,
/// <see cref="ListTldConfigsTests"/>, <see cref="UpdateTldConfigTests"/> and
/// <see cref="DeleteTldConfigTests"/> each add their own id-refusal coverage.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateTldConfigTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create, read, list, update and delete a TLD configuration.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateReadUpdateAndDeleteATldConfigAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, createBody) = DomainsTestSeeds.NewTldBody();

        var createResponse = await admin.PostAsJsonAsync("/api/admin/tld-configs", createBody);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await createResponse.Content.ReadFromJsonAsync<int>();

        var getResponse = await admin.GetAsync($"/api/admin/tld-configs/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var got = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        got.GetProperty("id").GetInt32().Should().Be(id);

        var listResponse = await admin.GetAsync("/api/admin/tld-configs");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Should().Contain(id);

        var updateResponse = await admin.PutAsJsonAsync($"/api/admin/tld-configs/{id}", new
        {
            registrarModule = "NameAm",
            currency = "AMD",
            sellCurrency = "USD",
            isEnabled = false,
            sortOrder = 2,
            categories = new[] { "updated" },
            costRegister = new Dictionary<string, decimal> { ["1"] = 6m },
            costTransfer = new Dictionary<string, decimal> { ["1"] = 6m },
            costRenew = new Dictionary<string, decimal> { ["1"] = 6m },
            sellRegister = new Dictionary<string, decimal> { ["1"] = 12m },
            sellTransfer = new Dictionary<string, decimal> { ["1"] = 12m },
            sellRenew = new Dictionary<string, decimal> { ["1"] = 12m },
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await (await admin.GetAsync($"/api/admin/tld-configs/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        afterUpdate.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        afterUpdate.GetProperty("sortOrder").GetInt32().Should().Be(2);

        var deleteResponse = await admin.DeleteAsync($"/api/admin/tld-configs/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await admin.GetAsync($"/api/admin/tld-configs/{id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.CodeOfAsync(afterDelete)).Should().Be(HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND);
    }

    /// <summary>Creating a second configuration for an already-configured extension answers 409 with <c>TLD_CONFIG_ALREADY_EXISTS</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesADuplicateExtensionWithAlreadyExistsAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, body) = DomainsTestSeeds.NewTldBody();
        (await admin.PostAsJsonAsync("/api/admin/tld-configs", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.PostAsJsonAsync("/api/admin/tld-configs", body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TLD_CONFIG_ALREADY_EXISTS);
    }

    /// <summary>An unrecognized registrar module answers 400 with <c>TLD_REGISTRAR_MODULE_UNKNOWN</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownRegistrarModuleAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (tld, _) = DomainsTestSeeds.NewTldBody();
        var withBadModule = new
        {
            tld,
            registrarModule = "NoSuchRegistrar",
            currency = "AMD",
            sellCurrency = "USD",
            isEnabled = true,
            sortOrder = 1,
            categories = new[] { "test" },
            costRegister = new Dictionary<string, decimal> { ["1"] = 5m },
            costTransfer = new Dictionary<string, decimal> { ["1"] = 5m },
            costRenew = new Dictionary<string, decimal> { ["1"] = 5m },
            sellRegister = new Dictionary<string, decimal> { ["1"] = 10m },
            sellTransfer = new Dictionary<string, decimal> { ["1"] = 10m },
            sellRenew = new Dictionary<string, decimal> { ["1"] = 10m },
        };

        var response = await admin.PostAsJsonAsync("/api/admin/tld-configs", withBadModule);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TLD_REGISTRAR_MODULE_UNKNOWN);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();
        var (_, body) = DomainsTestSeeds.NewTldBody();

        var response = await client.PostAsJsonAsync("/api/admin/tld-configs", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
