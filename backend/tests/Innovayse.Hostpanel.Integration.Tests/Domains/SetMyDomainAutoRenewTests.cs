using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>SetMyDomainAutoRenew</c> use case:
/// <c>PUT /api/me/domains/{id}/auto-renew</c>. The admin equivalent is <c>SetAutoRenew</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetMyDomainAutoRenewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Auto-renew refuses a stranger and applies for the owner: the handler dispatches through
    /// <c>IDomainOwnership</c>'s <see cref="Innovayse.Hostpanel.Shared.Models.Result"/>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_RefusesAStrangerAndAppliesForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "set-my-domain-auto-renew");

        var strangersAttempt = await stranger.PutAsJsonAsync(
            $"/api/me/domains/{domainId}/auto-renew", new { value = true });
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PutAsJsonAsync(
            $"/api/me/domains/{domainId}/auto-renew", new { value = true });
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "setmydomainautorenew-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/auto-renew",
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>A body without <c>value</c> is refused with 400 <c>VALIDATION_ERROR</c>, not read as <c>false</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_WithoutValue_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "setmydomainautorenew-novalue");

        var response = await client.PutAsJsonAsync("/api/me/domains/1/auto-renew", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// The client route changes the panel's renewal flag only: the registrar is never asked, because the registrar's own
    /// auto renew is a separate setting that only staff change.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_ChangesThePanelFlagAndNeverCallsTheRegistrarAsync()
    {
        var registrar = new RecordingRegistrar(new ManualTimeProvider(TestMoments.Seeded));
        var (_, owner, domainId, _) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "set-my-domain-no-registrar");
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRegistrarProvider>(registrar)));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await client.PutAsJsonAsync($"/api/me/domains/{domainId}/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        registrar.Calls.Should().BeEmpty();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Domains.AsNoTracking().SingleAsync(d => d.Id == domainId)).AutoRenew.Should().BeTrue();
    }
}
