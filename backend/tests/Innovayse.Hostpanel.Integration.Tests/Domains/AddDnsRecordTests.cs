using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>AddDnsRecord</c> use case:
/// <c>POST /api/domains/{id}/dns</c>. The client-facing equivalent is
/// <c>AddMyDomainDnsRecord</c>; the sibling use cases in the same zone lifecycle are
/// <c>UpdateDnsRecord</c> and <c>DeleteDnsRecord</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddDnsRecordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// <c>POST /api/domains/{id}/dns</c> with <c>{ type, host, value, ttl, priority }</c>, the exact
    /// shape <c>DnsRecordsTable.vue</c>'s <c>saveAdd</c> sends, succeeds with 201, and the record
    /// round-trips through <c>GET /api/domains/{id}</c>. Then <c>PUT /api/domains/{id}/dns/{recordId}</c>
    /// with the same shape updates it, and <c>DELETE</c> removes it: the full lifecycle the admin
    /// table drives.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddUpdateDeleteDnsRecord_FullLifecycle_SucceedsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var addResponse = await client.PostAsJsonAsync(
            $"/api/domains/{domainId}/dns",
            new { type = "A", host = "www", value = "1.2.3.4", ttl = 3600, priority = (int?)null });
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var domainAfterAdd = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        var records = domainAfterAdd.GetProperty("dnsRecords").EnumerateArray().ToList();
        records.Should().ContainSingle();
        var recordId = records[0].GetProperty("id").GetInt32();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/domains/{domainId}/dns/{recordId}",
            new { value = "5.6.7.8", ttl = 1800, priority = (int?)null });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var domainAfterUpdate = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        domainAfterUpdate.GetProperty("dnsRecords")[0].GetProperty("value").GetString().Should().Be("5.6.7.8");

        var deleteResponse = await client.DeleteAsync($"/api/domains/{domainId}/dns/{recordId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var domainAfterDelete = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        domainAfterDelete.GetProperty("dnsRecords").GetArrayLength().Should().Be(0);
    }

    /// <summary>An MX record with no priority refuses with 400 by the validator's own rule.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecord_MxWithoutPriority_Returns400Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsJsonAsync(
            $"/api/domains/{domainId}/dns",
            new { type = "MX", host = "@", value = "mail.example.com", ttl = 3600, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Adding a DNS record to an unknown domain answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecord_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/dns",
            new { type = "A", host = "www", value = "1.2.3.4", ttl = 3600, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecord_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/dns",
            new { type = "A", host = "www", value = "1.2.3.4", ttl = 3600, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
