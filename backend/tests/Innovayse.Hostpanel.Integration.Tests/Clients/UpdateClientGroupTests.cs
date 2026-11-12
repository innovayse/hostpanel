using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>Integration tests for the <c>UpdateClientGroup</c> use case: <c>PUT /api/admin/client-groups/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateClientGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces the name, description, discount and colour, and leaves the default flag alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesTheFieldsAndKeepsTheDefaultFlag()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, true);

        var response = await admin.PutAsJsonAsync(
            $"{ClientGroupTestSeeds.Route}/{id}", ClientGroupTestSeeds.Body("Renamed", null, 25m, "#10b981"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var group = await ClientGroupTestSeeds.GetAsync(admin, id);
        group.GetProperty("name").GetString().Should().Be("Renamed");
        group.GetProperty("description").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        group.GetProperty("discountPercent").GetDecimal().Should().Be(25m);
        group.GetProperty("color").GetString().Should().Be("#10B981");
        group.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        await ClientGroupTestSeeds.SetDefaultAsync(admin, id, false);
    }

    /// <summary>An id that names no group answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"{ClientGroupTestSeeds.Route}/2147483000", ClientGroupTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
    }

    /// <summary>A discount with more than two decimals is refused, since the column would round it, and the stored group keeps its old discount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesADiscountWithMoreThanTwoDecimals()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin, discountPercent: 5m);

        var response = await admin.PutAsJsonAsync(
            $"{ClientGroupTestSeeds.Route}/{id}", ClientGroupTestSeeds.Body(discountPercent: 12.345m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
        (await ClientGroupTestSeeds.GetAsync(admin, id)).GetProperty("discountPercent").GetDecimal().Should().Be(5m);
    }

    /// <summary>A discount above 100 percent is refused and the stored group keeps its old discount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnInvalidDiscount()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin, discountPercent: 5m);

        var response = await admin.PutAsJsonAsync(
            $"{ClientGroupTestSeeds.Route}/{id}", ClientGroupTestSeeds.Body(discountPercent: 101m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
        (await ClientGroupTestSeeds.GetAsync(admin, id)).GetProperty("discountPercent").GetDecimal().Should().Be(5m);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{ClientGroupTestSeeds.Route}/1", ClientGroupTestSeeds.Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Renaming a group to a name another group has is refused with 409; it keeps its name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesANameAnotherGroupHas()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var taken = $"Taken {Guid.NewGuid():N}";
        await ClientGroupTestSeeds.CreateAsync(admin, taken);
        var own = $"Own {Guid.NewGuid():N}";
        var id = await ClientGroupTestSeeds.CreateAsync(admin, own);

        var response = await admin.PutAsJsonAsync(
            $"{ClientGroupTestSeeds.Route}/{id}", ClientGroupTestSeeds.Body(taken));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NAME_ALREADY_EXISTS);
        (await ClientGroupTestSeeds.GetAsync(admin, id)).GetProperty("name").GetString().Should().Be(own);
    }

    /// <summary>Saving a group again under its own name is not a clash with itself.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_KeepingTheOwnNameIsAccepted()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var name = $"Own {Guid.NewGuid():N}";
        var id = await ClientGroupTestSeeds.CreateAsync(admin, name);

        var response = await admin.PutAsJsonAsync(
            $"{ClientGroupTestSeeds.Route}/{id}", ClientGroupTestSeeds.Body(name, discountPercent: 7m));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
