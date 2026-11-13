using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>Integration tests for <c>GET /api/domains/renewals/upcoming</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListUpcomingDomainRenewalsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds an active domain expiring in the given number of days.</summary>
    /// <param name="inDays">Days from now to its expiry.</param>
    /// <param name="autoRenew">Whether it renews automatically.</param>
    /// <returns>The domain id.</returns>
    private async Task<int> SeedAsync(int inDays, bool autoRenew)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var domain = RegisteredDomain.Register(
            client.Id, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, DateTimeOffset.UtcNow.AddDays(inDays),
            autoRenew: autoRenew, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain.Id;
    }

    /// <summary>Reads the ids of the domains on a page of the endpoint, with each auto-renew flag.</summary>
    /// <param name="window">The window name on the wire.</param>
    /// <returns>The flag of each listed domain, by id.</returns>
    private async Task<Dictionary<int, bool>> ListAsync(string window)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"/api/domains/renewals/upcoming?window={window}&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(e => e.GetProperty("id").GetInt32(), e => e.GetProperty("autoRenew").GetBoolean());
    }

    /// <summary>A wider window brings in later expiries, and a domain with auto-renew off is listed with its switch off.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Windows_BringInLaterExpiries_AndKeepAutoRenewOffVisible()
    {
        var soon = await SeedAsync(10, autoRenew: true);
        var later = await SeedAsync(45, autoRenew: false);
        var farOff = await SeedAsync(75, autoRenew: true);

        var in30 = await ListAsync("Next30Days");
        var in60 = await ListAsync("Next60Days");
        var in90 = await ListAsync("Next90Days");

        in30.Keys.Should().Contain(soon).And.NotContain(later).And.NotContain(farOff);
        in60.Keys.Should().Contain([soon, later]).And.NotContain(farOff);
        in90.Keys.Should().Contain([soon, later, farOff]);
        in60[later].Should().BeFalse();
        in60[soon].Should().BeTrue();
    }

    /// <summary>A window that is not one of the three is refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownWindow_Returns400()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains/renewals/upcoming?window=45");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Without authentication the call is refused with 401, and a client who is not staff gets 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnlyStaffMayList()
    {
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        (await factory.CreateClient().GetAsync("/api/domains/renewals/upcoming")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stranger.GetAsync("/api/domains/renewals/upcoming")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
