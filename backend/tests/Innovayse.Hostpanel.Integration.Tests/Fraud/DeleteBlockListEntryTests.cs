using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary><c>DELETE /api/admin/fraud/blocklist/{id}</c> against Postgres: the removal, the missing entry and the authorization boundary.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteBlockListEntryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A removed entry is gone from the list, and the value can be listed again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovesTheEntry()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "SY" });
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.DeleteAsync($"/api/admin/fraud/blocklist/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var page = await admin.GetFromJsonAsync<JsonElement>("/api/admin/fraud/blocklist?type=Country&page=1&pageSize=100");
        page.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).Should().NotContain(id);
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "SY" })).StatusCode
            .Should().Be(HttpStatusCode.Created);
    }

    /// <summary>An id nothing names answers 404 with <c>FRAUD_BLOCKLIST_ENTRY_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingEntryRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync("/api/admin/fraud/blocklist/987654321");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.FRAUD_BLOCKLIST_ENTRY_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NonPositiveIdIsRefused()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync("/api/admin/fraud/blocklist/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Removing from the block list is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/admin/fraud/blocklist/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.DeleteAsync("/api/admin/fraud/blocklist/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
