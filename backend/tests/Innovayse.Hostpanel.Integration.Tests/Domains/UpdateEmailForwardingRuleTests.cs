using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateEmailForwardingRule;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>UpdateEmailForwardingRule</c> use case:
/// <c>PUT /api/domains/{id}/email-forwarding/{ruleId}</c>. The sibling use cases in the same
/// lifecycle are <c>AddEmailForwardingRule</c> and <c>DeleteEmailForwardingRule</c>; the
/// client-facing equivalent is <c>UpdateMyDomainEmailForwardingRule</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateEmailForwardingRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The full admin lifecycle: add with <c>{ source, destination }</c> (no <c>isActive</c>),
    /// then update with the same two fields and no <c>isActive</c>, exactly what
    /// <c>EmailForwardingTable.vue</c>'s plain edit form sends, and the rule stays active,
    /// because <see cref="UpdateEmailForwardingRuleCommand"/> defaults <c>IsActive</c> to
    /// <see langword="true"/>.
    /// Then delete.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddUpdateDeleteEmailForwardingRule_OmittedIsActiveStaysTrue_SucceedsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var addResponse = await client.PostAsJsonAsync(
            $"/api/domains/{domainId}/email-forwarding", new { source = "info", destination = "info@example.test" });
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var domainAfterAdd = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        var rules = domainAfterAdd.GetProperty("emailForwardingRules").EnumerateArray().ToList();
        rules.Should().ContainSingle();
        var ruleId = rules[0].GetProperty("id").GetInt32();

        // The plain edit form never sends isActive; only the dedicated toggle does.
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/domains/{domainId}/email-forwarding/{ruleId}",
            new { source = "sales", destination = "sales@example.test" });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var domainAfterUpdate = await client.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        var updatedRule = domainAfterUpdate.GetProperty("emailForwardingRules")[0];
        updatedRule.GetProperty("destination").GetString().Should().Be("sales@example.test");
        updatedRule.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var deleteResponse = await client.DeleteAsync($"/api/domains/{domainId}/email-forwarding/{ruleId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a rule on an unknown domain answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateEmailForwardingRule_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/email-forwarding/1",
            new { source = "sales", destination = "sales@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateEmailForwardingRule_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/email-forwarding/1",
            new { source = "sales", destination = "sales@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive rule id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateEmailForwardingRule_NonPositiveRuleId_Returns400ValidationErrorAsync()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/email-forwarding/0",
            new { source = "info", destination = "info@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
