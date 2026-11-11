using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.IntegrationEvents.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// How clients get into a client group: staff assign one on update or create, a client created without
/// one joins the default group, the client list filters by group, and the client reads show the group.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientGroupAssignmentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating a client with a group id puts it in the group, and the client reads show the group's id, name and colour.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_AssignsTheGroupAndTheReadsShowIt()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var groupId = await ClientGroupTestSeeds.CreateAsync(admin, "Assigned");

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(clientGroupId: groupId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("clientGroupId").GetInt32().Should().Be(groupId);
        fetched.GetProperty("clientGroupName").GetString().Should().Be("Assigned");
        fetched.GetProperty("clientGroupColor").GetString().Should().Be("#3B82F6");
        var listed = await ListAsync(admin, $"page=1&pageSize=100&clientGroupId={groupId}");
        var row = listed.Should().ContainSingle().Subject;
        row.GetProperty("id").GetInt32().Should().Be(clientId);
        row.GetProperty("clientGroupName").GetString().Should().Be("Assigned");
        row.GetProperty("clientGroupColor").GetString().Should().Be("#3B82F6");
    }

    /// <summary>Updating a client without a group id takes it out of its group.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_WithoutAGroupClearsIt()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        await ClientGroupTestSeeds.AssignAsync(factory, clientId, await ClientGroupTestSeeds.CreateAsync(admin));

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("clientGroupId").ValueKind.Should().Be(JsonValueKind.Null);
        fetched.GetProperty("clientGroupName").ValueKind.Should().Be(JsonValueKind.Null);
        fetched.GetProperty("clientGroupColor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A group id that names no group is refused with 404 and the client keeps what it had.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownGroup_RefusesWithNotFoundCode()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Keeper");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Changed", clientGroupId: 2147483000));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("firstName").GetString().Should().Be("Keeper");
    }

    /// <summary>A client editing their own profile does not lose the group staff put them in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfile_KeepsTheGroup()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "group-profile");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var groupId = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.AssignAsync(factory, clientId, groupId);

        var response = await client.PatchAsJsonAsync("/api/clients/me", new { firstName = "Renamed" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("clientGroupId").GetInt32().Should().Be(groupId);
    }

    /// <summary>A client created by staff with a group id joins that group, even when another group is the default.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCreate_WithAGroup_JoinsIt()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "group-create-explicit");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var defaultGroup = await ClientGroupTestSeeds.CreateAsync(admin);
        var chosen = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, defaultGroup, true);

        var created = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId, clientGroupId: chosen));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clientId = await created.Content.ReadFromJsonAsync<int>();
        (await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}")).GetProperty("clientGroupId").GetInt32().Should().Be(chosen);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, defaultGroup, false);
    }

    /// <summary>A client created by staff without a group joins the default group.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCreate_WithoutAGroup_JoinsTheDefaultGroup()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "group-create-default");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var defaultGroup = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, defaultGroup, true);

        var created = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clientId = await created.Content.ReadFromJsonAsync<int>();
        (await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}")).GetProperty("clientGroupId").GetInt32().Should().Be(defaultGroup);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, defaultGroup, false);
    }

    /// <summary>With no default group, a client created by staff without a group has none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCreate_WithoutAGroupAndNoDefault_HasNoGroup()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "group-create-none");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clientId = await created.Content.ReadFromJsonAsync<int>();
        (await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}")).GetProperty("clientGroupId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A group id that names no group is refused with 404 before any client is created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCreate_UnknownGroup_RefusesWithNotFoundCode()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "group-create-unknown");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId, clientGroupId: 2147483000));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Clients.AnyAsync(c => c.UserId == subject.UserId)).Should().BeFalse();
    }

    /// <summary>A client created by registering joins the default group.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_JoinsTheDefaultGroup()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var defaultGroup = await ClientGroupTestSeeds.CreateAsync(admin);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, defaultGroup, true);
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "group-register");
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(subject.UserId, subject.Email, "Grace", "Hopper"));

        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(c => c.UserId == subject.UserId);
        client.ClientGroupId.Should().Be(defaultGroup);
        await ClientGroupTestSeeds.SetDefaultAsync(admin, defaultGroup, false);
    }

    /// <summary>The client list filters by group: another group's clients and ungrouped clients are left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_FiltersByGroup()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var groupId = await ClientGroupTestSeeds.CreateAsync(admin);
        var otherGroupId = await ClientGroupTestSeeds.CreateAsync(admin);
        var (member, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (stranger, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (ungrouped, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientGroupTestSeeds.AssignAsync(factory, member, groupId);
        await ClientGroupTestSeeds.AssignAsync(factory, stranger, otherGroupId);

        var listed = await ListAsync(admin, $"page=1&pageSize=100&clientGroupId={groupId}");

        var ids = listed.Select(i => i.GetProperty("id").GetInt32()).ToList();
        ids.Should().Equal(member);
        ids.Should().NotContain([stranger, ungrouped]);
    }

    /// <summary>A group id that is not positive is refused by the list's validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_NonPositiveGroupId_RefusesWithValidationError()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/clients?page=1&pageSize=10&clientGroupId=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The rows of one page of the admin client list for the given query string.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The list rows.</returns>
    private static async Task<List<JsonElement>> ListAsync(HttpClient admin, string queryString)
    {
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/clients?{queryString}");
        return page.GetProperty("items").EnumerateArray().ToList();
    }
}
