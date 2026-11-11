using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>AddEmailForwardingRule</c> use case:
/// <c>POST /api/domains/{id}/email-forwarding</c>. The full add/update/delete lifecycle lives in
/// <see cref="UpdateEmailForwardingRuleTests"/>; these tests cover this use case's own refusals
/// directly. The
/// client-facing equivalent is <c>AddMyDomainEmailForwardingRule</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddEmailForwardingRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Adding a rule succeeds with 201 and the rule shows up on a read of the domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_ExistingDomain_Returns201AndPersistsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{domainId}/email-forwarding", new { source = "info", destination = "info@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var domain = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        domain.GetProperty("emailForwardingRules").EnumerateArray().Should().ContainSingle();
    }

    /// <summary>Adding a rule to an unknown domain answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/email-forwarding",
            new { source = "info", destination = "info@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/email-forwarding",
            new { source = "info", destination = "info@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive domain id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_NonPositiveDomainId_Returns400ValidationErrorAsync()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/domains/0/email-forwarding", new { source = "info", destination = "info@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
