using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary><c>POST /api/admin/fraud/blocklist</c> against Postgres: each type, the stored spelling, the duplicate and shape refusals and the authorization boundary.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddBlockListEntryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Each type is accepted and stored in its canonical spelling, which the list then shows.</summary>
    /// <param name="type">The type name on the wire.</param>
    /// <param name="typed">The value as the admin typed it.</param>
    /// <param name="stored">The spelling the list shows.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Ip", "198.51.100.7", "198.51.100.7")]
    [InlineData("Cidr", "198.51.100.77/24", "198.51.100.0/24")]
    [InlineData("EmailDomain", "@Spam-Domain.Example", "spam-domain.example")]
    [InlineData("Country", "kp", "KP")]
    public async Task AddsAnEntryInItsCanonicalSpelling(string type, string typed, string stored)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type, value = typed, note = "seen abusing checkout" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var id = await response.Content.ReadFromJsonAsync<int>();
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/fraud/blocklist?type={type}&page=1&pageSize=100");
        var entry = page.GetProperty("items").EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == id);
        entry.GetProperty("value").GetString().Should().Be(stored);
        entry.GetProperty("type").GetString().Should().Be(type);
        entry.GetProperty("note").GetString().Should().Be("seen abusing checkout");
    }

    /// <summary>The same value cannot be listed twice for a type, however it is spelled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheSameValueTwice()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "EmailDomain", value = "twice.example" })).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var response = await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "EmailDomain", value = "@TWICE.example" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.FRAUD_BLOCKLIST_ENTRY_EXISTS);
    }

    /// <summary>A value that does not fit its type is refused before anything is stored.</summary>
    /// <param name="type">The type name on the wire.</param>
    /// <param name="value">The value as typed.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Cidr", "198.51.100.7")]
    [InlineData("Cidr", "198.51.100.0/40")]
    [InlineData("Ip", "not an address")]
    [InlineData("EmailDomain", "no-dot")]
    [InlineData("Country", "KPX")]
    [InlineData("Ip", "")]
    public async Task RefusesAValueThatDoesNotFitItsType(string type, string value)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type, value });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A note past its column is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesATooLongNote()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "IR", note = new string('x', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Adding to the block list is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "IR" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "IR" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
