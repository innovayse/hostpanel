using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>ListTldConfigs</c> use case:
/// <c>GET /api/admin/tld-configs</c>. The happy path (create then list) lives in
/// <see cref="CreateTldConfigTests"/>; this file covers the empty and authorization cases.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTldConfigsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Listing never refuses: it answers 200 even before any configuration is created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsOkEvenWithNoConfigurationsCreatedByThisTestAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/admin/tld-configs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A newly created configuration shows up in the list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IncludesANewlyCreatedConfigurationAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (tld, body) = DomainsTestSeeds.NewTldBody();
        await admin.PostAsJsonAsync("/api/admin/tld-configs", body);

        var response = await admin.GetAsync("/api/admin/tld-configs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(tld);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/tld-configs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
