using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// <c>GET /api/public/demo</c>: anonymous; on a demo deployment it publishes the two accounts and
/// the next reset time, otherwise the fixed "not a demo" answer with nothing in it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDemoTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A deployment that is not a demo answers <c>{ enabled: false, nextResetAt: null, accounts: [] }</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenDisabled_AnswersTheEmptyShapeAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/demo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        body.GetProperty("nextResetAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("accounts").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// A demo deployment publishes both accounts with the role as a string, and a reset time about
    /// one interval ahead, with no credential presented.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_PublishesTheAccountsAndTheNextResetAsync()
    {
        var host = DemoTestHost.Enabled(factory);

        var response = await host.CreateClient().GetAsync("/api/public/demo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("enabled").GetBoolean().Should().BeTrue();

        var nextResetAt = body.GetProperty("nextResetAt").GetDateTimeOffset();
        nextResetAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddHours(DemoTestHost.ResetIntervalHours), TimeSpan.FromMinutes(5));

        var accounts = body.GetProperty("accounts").EnumerateArray().ToList();
        accounts.Should().HaveCount(2);
        accounts.Select(a => a.GetProperty("role").GetString()).Should().Equal("Admin", "Client");
        accounts[0].GetProperty("email").GetString().Should().Be(DemoTestHost.AdminEmail);
        accounts[0].GetProperty("password").GetString().Should().Be(DemoTestHost.AdminPassword);
        accounts[1].GetProperty("email").GetString().Should().Be(DemoTestHost.ClientEmail);
        accounts[1].GetProperty("password").GetString().Should().Be(DemoTestHost.ClientPassword);
    }
}
