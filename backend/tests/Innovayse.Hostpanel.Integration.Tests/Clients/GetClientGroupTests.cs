using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>Integration tests for the <c>GetClientGroup</c> use case: <c>GET /api/admin/client-groups/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A group reads back with its fields and its client count.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_AnswersTheGroupWithItsClientCount()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin, "Partners", 15m);
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientGroupTestSeeds.AssignAsync(factory, clientId, id);

        var group = await ClientGroupTestSeeds.GetAsync(admin, id);

        group.GetProperty("id").GetInt32().Should().Be(id);
        group.GetProperty("name").GetString().Should().Be("Partners");
        group.GetProperty("discountPercent").GetDecimal().Should().Be(15m);
        group.GetProperty("clientCount").GetInt32().Should().Be(1);
    }

    /// <summary>An id that names no group answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{ClientGroupTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_NonPositiveId_RefusesWithValidationError()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{ClientGroupTestSeeds.Route}/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{ClientGroupTestSeeds.Route}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
