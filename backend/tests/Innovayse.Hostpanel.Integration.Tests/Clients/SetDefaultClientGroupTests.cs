using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>Integration tests for the <c>SetDefaultClientGroup</c> use case: <c>PUT /api/admin/client-groups/{id}/default</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetDefaultClientGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Making a second group the default clears the flag on the first, so exactly one group holds it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_ClearsTheFlagOnTheGroupThatHeldIt()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var first = await ClientGroupTestSeeds.CreateAsync(admin);
        var second = await ClientGroupTestSeeds.CreateAsync(admin);

        await ClientGroupTestSeeds.SetDefaultAsync(admin, first, true);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, second, true);

        (await ClientGroupTestSeeds.GetAsync(admin, first)).GetProperty("isDefault").GetBoolean().Should().BeFalse();
        (await ClientGroupTestSeeds.GetAsync(admin, second)).GetProperty("isDefault").GetBoolean().Should().BeTrue();
        var all = await admin.GetFromJsonAsync<JsonElement>(ClientGroupTestSeeds.Route);
        all.EnumerateArray().Count(g => g.GetProperty("isDefault").GetBoolean()).Should().Be(1);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, second, false);
    }

    /// <summary>Making the group that is already the default the default again changes nothing and still answers 204.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_OnTheCurrentDefault_IsANoOp()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, true);

        var response = await admin.PutAsJsonAsync($"{ClientGroupTestSeeds.Route}/{id}/default", new { isDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ClientGroupTestSeeds.GetAsync(admin, id)).GetProperty("isDefault").GetBoolean().Should().BeTrue();
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, false);
    }

    /// <summary>Clearing the flag leaves no group as the default.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_False_LeavesNoDefault()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, true);

        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, false);

        var all = await admin.GetFromJsonAsync<JsonElement>(ClientGroupTestSeeds.Route);
        all.EnumerateArray().Any(g => g.GetProperty("isDefault").GetBoolean()).Should().BeFalse();
    }

    /// <summary>An id that names no group answers 404 with the not-found code, and the current default is untouched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, true);

        var response = await admin.PutAsJsonAsync($"{ClientGroupTestSeeds.Route}/2147483000/default", new { isDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        (await ClientGroupTestSeeds.GetAsync(admin, id)).GetProperty("isDefault").GetBoolean().Should().BeTrue();
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, false);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDefault_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{ClientGroupTestSeeds.Route}/1/default", new { isDefault = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
