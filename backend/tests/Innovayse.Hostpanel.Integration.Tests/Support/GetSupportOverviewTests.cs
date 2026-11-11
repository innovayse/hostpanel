using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/tickets/overview</c>: staff can read the aggregated support overview for a time
/// period, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSupportOverviewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can read the support overview.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadTheOverviewAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/tickets/overview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Every wire name of a period, in any case, binds and is answered.</summary>
    /// <param name="period">The period as the query string spells it.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("today")]
    [InlineData("yesterday")]
    [InlineData("last7days")]
    [InlineData("Last30Days")]
    public async Task EveryNamedPeriodIsAnsweredAsync(string period)
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/tickets/overview?period={period}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A period that names no member, or a number outside the set, is refused with 400.</summary>
    /// <param name="period">The refused value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("fortnight")]
    [InlineData("42")]
    public async Task AnUnknownPeriodIsRefusedAsync(string period)
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/tickets/overview?period={period}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with no credential cannot reach the staff overview report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OverviewIsClosedToNonStaffCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets/overview");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
