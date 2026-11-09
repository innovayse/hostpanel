using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>DeleteDnsRecord</c> use case:
/// <c>DELETE /api/domains/{id}/dns/{recordId}</c>. The full add/update/delete lifecycle lives in
/// <see cref="AddDnsRecordTests"/>; these tests cover this use case's own refusals directly.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteDnsRecordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a DNS record on an unknown domain answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteDnsRecord_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/domains/{DomainsTestSeeds.UnknownId}/dns/1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unknown record id on a real domain answers 404 with <c>DNS_RECORD_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteDnsRecord_UnknownRecord_Returns404WithDnsRecordNotFoundAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.DeleteAsync($"/api/domains/{domainId}/dns/{DomainsTestSeeds.UnknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DNS_RECORD_NOT_FOUND);
    }

    /// <summary>Deleting a record that exists succeeds and removes it from the zone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteDnsRecord_ExistingRecord_Returns204AndRemovesItAsync()
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
        var recordId = domainAfterAdd.GetProperty("dnsRecords")[0].GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/domains/{domainId}/dns/{recordId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var domainAfterDelete = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        domainAfterDelete.GetProperty("dnsRecords").GetArrayLength().Should().Be(0);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteDnsRecord_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/domains/{DomainsTestSeeds.UnknownId}/dns/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive record id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteDnsRecord_NonPositiveRecordId_Returns400ValidationErrorAsync()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/domains/{DomainsTestSeeds.UnknownId}/dns/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
