using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>SetAutoRenew</c> use case: the admin
/// <c>PUT /api/domains/{id}/auto-renew</c>. The client-facing equivalent is
/// <c>SetMyDomainAutoRenew</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetAutoRenewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Registers a domain, then calls <c>PUT /api/domains/{id}/auto-renew</c> with
    /// <c>value=true</c>. Expects 204 No Content.
    /// </summary>
    [Fact]
    public async Task SetAutoRenew_ValueTrue_Returns204Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "set-auto-renew");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        var response = await admin.PutAsJsonAsync(
            $"/api/domains/{domainId}/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// The body field is <c>value</c>: <c>{ value: true }</c> switches auto-renew on and
    /// <c>{ value: false }</c> switches it off, read back through <c>GET /api/domains/{id}</c>.
    /// </summary>
    [Fact]
    public async Task SetAutoRenew_ValueField_TogglesTheFlagAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "set-auto-renew-value");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        (await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { value = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAutoRenewAsync(admin, domainId)).Should().BeTrue();

        (await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { value = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAutoRenewAsync(admin, domainId)).Should().BeFalse();
    }

    /// <summary>
    /// A body that names the flag <c>enabled</c> instead of <c>value</c> is refused with 400
    /// <c>VALIDATION_ERROR</c>, not read as <c>false</c>: a domain that had auto-renew on keeps it.
    /// </summary>
    [Fact]
    public async Task SetAutoRenew_EnabledFieldInsteadOfValue_Returns400AndKeepsTheFlagAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "set-auto-renew-enabled");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);
        await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { value = true });
        (await ReadAutoRenewAsync(admin, domainId)).Should().BeTrue();

        var response = await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
        (await ReadAutoRenewAsync(admin, domainId)).Should().BeTrue();
    }

    /// <summary>An empty body is refused with 400 <c>VALIDATION_ERROR</c> as well.</summary>
    [Fact]
    public async Task SetAutoRenew_EmptyBody_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "set-auto-renew-empty");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        var response = await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// A staff change writes one activity log line on the owning client, naming the domain id and
    /// the new value and nothing else about the client.
    /// </summary>
    [Fact]
    public async Task SetAutoRenew_StaffChange_RecordsAnActivityLogLineAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "set-auto-renew-log");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        (await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { value = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { value = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var lines = await context.ActivityLogs.AsNoTracking()
            .Where(l => l.ClientId == clientId && l.Description.StartsWith("Set Domain Auto Renew"))
            .Select(l => l.Description)
            .ToListAsync();
        lines.Should().BeEquivalentTo(
            $"Set Domain Auto Renew - Domain ID: {domainId}, Auto Renew: True",
            $"Set Domain Auto Renew - Domain ID: {domainId}, Auto Renew: False");
    }

    /// <summary>A refused body writes no activity log line.</summary>
    [Fact]
    public async Task SetAutoRenew_RefusedBody_RecordsNoActivityLogLineAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "set-auto-renew-nolog");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { enabled = true });

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityLogs.AsNoTracking()
            .AnyAsync(l => l.ClientId == clientId && l.Description.StartsWith("Set Domain Auto Renew")))
            .Should().BeFalse();
    }

    /// <summary>Reads the auto-renew switch of a domain through the admin detail route.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="domainId">The domain's primary key.</param>
    /// <returns>The current auto-renew flag.</returns>
    private static async Task<bool> ReadAutoRenewAsync(HttpClient admin, int domainId)
    {
        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        return body.GetProperty("features").GetProperty("autoRenew").GetBoolean();
    }

    /// <summary>An id no domain ever took answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task SetAutoRenew_UnknownDomain_Returns404Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    [Fact]
    public async Task SetAutoRenew_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive domain id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    [Fact]
    public async Task SetAutoRenew_NonPositiveDomainId_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync("/api/domains/0/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
