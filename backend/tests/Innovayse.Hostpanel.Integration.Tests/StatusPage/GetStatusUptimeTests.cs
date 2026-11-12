using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>GetStatusUptime</c> use case: <c>GET /api/public/status/uptime</c>, the uptime of every
/// component over 30, 60 or 90 days with a daily series for the uptime bars. Anonymous.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetStatusUptimeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the clocked host stands at, so the days of the window are known.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Seeds a monitored component and one record per entry, each <c>days ago</c> at noon.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="checks">The records to store: how many days before now, and whether the check succeeded.</param>
    /// <returns>The id of the new component.</returns>
    private static async Task<int> SeedAsync(ClockedHost host, params (int DaysAgo, bool Succeeded)[] checks)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = StatusComponentGroup.Create($"Uptime {Guid.NewGuid():N}", 0);
        var component = group.AddComponent("Web hosting", null, 0);
        component.ConfigureMonitoring("https://hosting.example.test/health", true, 3);
        context.StatusComponentGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        foreach (var (daysAgo, succeeded) in checks)
        {
            context.StatusChecks.Add(component.RecordCheck(Now.AddDays(-daysAgo), succeeded, 20));
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return component.Id;
    }

    /// <summary>Reads the uptime of one component from the public feed.</summary>
    /// <param name="host">The host to call.</param>
    /// <param name="route">The route with its query.</param>
    /// <param name="id">The component to find.</param>
    /// <returns>The document and the component's element in it.</returns>
    private static async Task<(JsonElement Document, JsonElement Component)> ReadAsync(ClockedHost host, string route, int id)
    {
        var response = await host.CreateClient().GetAsync(route);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
        return (document, document.GetProperty("components").EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == id));
    }

    /// <summary>The figure is the share of the window's checks that succeeded, and each day carries its own share.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_CountsTheChecksOfTheWindow_PerComponentAndPerDay()
    {
        using var host = new ClockedHost(factory, Now);
        var id = await SeedAsync(host, (0, true), (0, true), (0, true), (0, false), (2, true), (2, true));

        var (document, component) = await ReadAsync(host, $"{StatusPageTestSeeds.UptimeRoute}?days=30", id);

        document.GetProperty("days").GetInt32().Should().Be(30);
        component.GetProperty("uptimePercent").GetDouble().Should().Be(83.333);
        var daily = component.GetProperty("daily").EnumerateArray().ToList();
        daily.Should().HaveCount(30);
        daily[^1].GetProperty("date").GetString().Should().Be("2026-11-11");
        daily[^1].GetProperty("uptimePercent").GetDouble().Should().Be(75);
        daily[^3].GetProperty("uptimePercent").GetDouble().Should().Be(100);
        daily[^2].GetProperty("uptimePercent").ValueKind.Should().Be(JsonValueKind.Null);
        daily[0].GetProperty("date").GetString().Should().Be("2026-10-13");
    }

    /// <summary>A wider window counts the older checks too, and the narrower one leaves them out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_WiderWindow_CountsOlderChecks()
    {
        using var host = new ClockedHost(factory, Now);
        var id = await SeedAsync(host, (0, true), (40, false));

        var (_, within30) = await ReadAsync(host, $"{StatusPageTestSeeds.UptimeRoute}?days=30", id);
        var (_, within60) = await ReadAsync(host, $"{StatusPageTestSeeds.UptimeRoute}?days=60", id);

        within30.GetProperty("uptimePercent").GetDouble().Should().Be(100);
        within60.GetProperty("uptimePercent").GetDouble().Should().Be(50);
        within60.GetProperty("daily").GetArrayLength().Should().Be(60);
    }

    /// <summary>A component with no check in the window has no figure and an empty series, not a made-up 100.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ComponentWithoutChecks_HasNoFigure()
    {
        using var host = new ClockedHost(factory, Now);
        var id = await SeedAsync(host);

        var (_, component) = await ReadAsync(host, StatusPageTestSeeds.UptimeRoute, id);

        component.GetProperty("uptimePercent").ValueKind.Should().Be(JsonValueKind.Null);
        component.GetProperty("daily").EnumerateArray().Should().OnlyContain(d => d.GetProperty("uptimePercent").ValueKind == JsonValueKind.Null);
    }

    /// <summary>With no window named, the feed answers 90 days.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_WithoutWindow_Answers90Days()
    {
        using var host = new ClockedHost(factory, Now);
        var id = await SeedAsync(host, (0, true));

        var (document, component) = await ReadAsync(host, StatusPageTestSeeds.UptimeRoute, id);

        document.GetProperty("days").GetInt32().Should().Be(90);
        component.GetProperty("daily").GetArrayLength().Should().Be(90);
    }

    /// <summary>A window other than 30, 60 or 90 is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <param name="days">A window the feed refuses.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(365)]
    public async Task Get_WindowOutOfSet_RefusesWithValidationError(int days)
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, $"{StatusPageTestSeeds.UptimeRoute}?days={days}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
