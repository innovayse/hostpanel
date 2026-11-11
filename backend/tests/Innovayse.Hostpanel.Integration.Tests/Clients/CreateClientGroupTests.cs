using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>Integration tests for the <c>CreateClientGroup</c> use case: <c>POST /api/admin/client-groups</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateClientGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a group answers 201 with its id, and the group reads back with the fields sent, upper-case colour and no clients.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_StoresTheGroupAsNotDefault()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            ClientGroupTestSeeds.Route, ClientGroupTestSeeds.Body("Wholesale", "Resellers", 12.5m, "#3b82f6"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        var group = await ClientGroupTestSeeds.GetAsync(admin, id);
        group.GetProperty("name").GetString().Should().Be("Wholesale");
        group.GetProperty("description").GetString().Should().Be("Resellers");
        group.GetProperty("discountPercent").GetDecimal().Should().Be(12.5m);
        group.GetProperty("color").GetString().Should().Be("#3B82F6");
        group.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        group.GetProperty("clientCount").GetInt32().Should().Be(0);
    }

    /// <summary>A name, discount or colour the rules refuse answers 400 and stores nothing.</summary>
    /// <param name="name">The name to send.</param>
    /// <param name="discountPercent">The discount to send.</param>
    /// <param name="color">The colour to send.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("", 10, "#3B82F6")]
    [InlineData("Valid", -0.01, "#3B82F6")]
    [InlineData("Valid", 100.01, "#3B82F6")]
    [InlineData("Valid", 10.005, "#3B82F6")]
    [InlineData("Valid", 10, "blue")]
    [InlineData("Valid", 10, "#3B82F")]
    [InlineData("Valid", 10, "#GGGGGG")]
    public async Task Create_RefusesAnInvalidField(string name, double discountPercent, string color)
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            ClientGroupTestSeeds.Route, ClientGroupTestSeeds.Body(name, null, (decimal)discountPercent, color));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(ClientGroupTestSeeds.Route, ClientGroupTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAClient()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "create-group-forbidden");

        var response = await client.PostAsJsonAsync(ClientGroupTestSeeds.Route, ClientGroupTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A name another group already has is refused with 409, and no second group is kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnExistingName()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var name = $"Taken {Guid.NewGuid():N}";
        await ClientGroupTestSeeds.CreateAsync(admin, name);

        var response = await admin.PostAsJsonAsync(ClientGroupTestSeeds.Route, ClientGroupTestSeeds.Body(name));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NAME_ALREADY_EXISTS);
        var groups = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>(ClientGroupTestSeeds.Route);
        groups.EnumerateArray().Count(g => g.GetProperty("name").GetString() == name).Should().Be(1);
    }
}
