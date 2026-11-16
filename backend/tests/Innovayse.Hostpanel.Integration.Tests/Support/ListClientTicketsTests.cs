using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/tickets/client/{clientId}</c>: staff can list a specific client's tickets and narrow the list by a
/// subject search and by status, priority and department, all before paging; the action is closed to a caller with
/// no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientTicketsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can list a specific client's tickets, each named after that client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StaffCanListAClientsTicketsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(client);
        var clientId = (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId;

        var ticketResponse = await client.PostAsJsonAsync("/api/tickets", new
        {
            clientId,
            subject = "Listing test ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        ticketResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"/api/tickets/client/{clientId}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Should().ContainSingle()
            .Which.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>The route id wins over a client id sent in the query, and paging binds from the query.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheRouteClientIdWinsOverTheQueryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/tickets/client/1?clientId=999&page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with no credential cannot list a client's tickets.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets/client/1?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Seeds a client with the four-ticket mix and another client with a ticket the same search would match.</summary>
    /// <returns>The mix of the client whose list is read.</returns>
    private async Task<TicketListMix> SeedMixAsync()
    {
        var owner = await ClientsTestSeeds.SeedClientAsync(factory);
        var mix = await SupportTestSeeds.SeedTicketMixAsync(factory, owner.ClientId);
        var other = await ClientsTestSeeds.SeedClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await SupportTestSeeds.SeedTicketAsync(
            context, other.ClientId, $"alpha-{mix.Token}", mix.DepartmentOne, TicketPriority.Low, TestMoments.Seeded);
        return mix;
    }

    /// <summary>The list holds only the route client's tickets, and the search matches the subject in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheSubjectOfThatClientOnlyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var list = $"/api/tickets/client/{mix.ClientId}?page=1&pageSize=100";

        var (all, allCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search={mix.Token}");
        var (alpha, alphaCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search=ALPHA-{mix.Token.ToUpperInvariant()}");

        all.Should().Equal(mix.Delta, mix.Gamma, mix.Beta, mix.Alpha);
        allCount.Should().Be(4);
        alpha.Should().Equal(mix.Alpha);
        alphaCount.Should().Be(1);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteralAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var owner = await ClientsTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        int percent;
        int underscore;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
            percent = await SupportTestSeeds.SeedTicketAsync(
                context, owner.ClientId, $"{token} 50% off", departmentId, TicketPriority.Low, TestMoments.Seeded);
            await SupportTestSeeds.SeedTicketAsync(
                context, owner.ClientId, $"{token} 50x off", departmentId, TicketPriority.Low, TestMoments.Seeded);
            underscore = await SupportTestSeeds.SeedTicketAsync(
                context, owner.ClientId, $"{token} a_c", departmentId, TicketPriority.Low, TestMoments.Seeded);
            await SupportTestSeeds.SeedTicketAsync(
                context, owner.ClientId, $"{token} abc", departmentId, TicketPriority.Low, TestMoments.Seeded);
        }

        var list = $"/api/tickets/client/{owner.ClientId}?page=1&pageSize=100";
        var (percentIds, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search={token}%2050%25");
        var (underscoreIds, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search={token}%20a_c");
        var (bareWildcard, bareCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search=%25%25");

        percentIds.Should().Equal(percent);
        underscoreIds.Should().Equal(underscore);
        bareWildcard.Should().BeEmpty();
        bareCount.Should().Be(0);
    }

    /// <summary>The status filter keeps only the client's tickets in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatusAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var list = $"/api/tickets/client/{mix.ClientId}?page=1&pageSize=100";

        var (open, openCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&status=Open");
        var (closed, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&status=Closed");
        var (awaiting, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&status=AwaitingReply");

        open.Should().Equal(mix.Gamma, mix.Alpha);
        openCount.Should().Be(2);
        closed.Should().Equal(mix.Beta);
        awaiting.Should().Equal(mix.Delta);
    }

    /// <summary>The priority filter keeps only the client's tickets of that priority.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PriorityFilterKeepsOnlyThatPriorityAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var list = $"/api/tickets/client/{mix.ClientId}?page=1&pageSize=100";

        var (high, highCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&priority=High");
        var (low, lowCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&priority=Low");

        high.Should().Equal(mix.Gamma, mix.Beta);
        highCount.Should().Be(2);
        low.Should().Equal(mix.Alpha);
        lowCount.Should().Be(1);
    }

    /// <summary>The department filter keeps only the client's tickets filed under that department.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DepartmentFilterKeepsOnlyThatDepartmentAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var list = $"/api/tickets/client/{mix.ClientId}?page=1&pageSize=100";

        var (one, oneCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&departmentId={mix.DepartmentOne}");
        var (two, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&departmentId={mix.DepartmentTwo}");

        one.Should().Equal(mix.Beta, mix.Alpha);
        oneCount.Should().Be(2);
        two.Should().Equal(mix.Delta, mix.Gamma);
    }

    /// <summary>The search and every filter narrow together: a ticket must pass each one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CombinedFiltersAllApplyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var list = $"/api/tickets/client/{mix.ClientId}?page=1&pageSize=100";

        var (openHighTwo, _) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"{list}&status=Open&priority=High&departmentId={mix.DepartmentTwo}");
        var (closedLow, closedLowCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&status=Closed&priority=Low");
        var (searchAndPriority, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search=-{mix.Token}&priority=High");
        var (searchOutsideStatus, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}&search=alpha-{mix.Token}&status=Closed");

        openHighTwo.Should().Equal(mix.Gamma);
        closedLow.Should().BeEmpty();
        closedLowCount.Should().Be(0);
        searchAndPriority.Should().Equal(mix.Gamma, mix.Beta);
        searchOutsideStatus.Should().BeEmpty();
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePagingAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var list = $"/api/tickets/client/{mix.ClientId}";

        var (first, firstCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}?page=1&pageSize=3");
        var (second, secondCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}?page=2&pageSize=3");
        var (beyond, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}?page=3&pageSize=3");
        var (highFirst, highCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}?page=1&pageSize=1&priority=High");
        var (highSecond, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{list}?page=2&pageSize=1&priority=High");

        first.Should().Equal(mix.Delta, mix.Gamma, mix.Beta);
        firstCount.Should().Be(4);
        second.Should().Equal(mix.Alpha);
        secondCount.Should().Be(4);
        beyond.Should().BeEmpty();
        highFirst.Should().Equal(mix.Gamma);
        highSecond.Should().Equal(mix.Beta);
        highCount.Should().Be(2);
    }

    /// <summary>A page below one or a page size outside the limit is refused with 400, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("pageSize=20")]
    [InlineData("page=1&pageSize=0")]
    [InlineData("page=1&pageSize=101")]
    public async Task PagingOutsideTheLimitsIsRefusedAsync(string queryString)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/tickets/client/1?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SupportTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A search longer than the limit is refused with 400, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefusedAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/tickets/client/1?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SupportTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A status, priority or department that names nothing, by name or by number, is refused with 400.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("status=Archived")]
    [InlineData("status=99")]
    [InlineData("priority=Urgent")]
    [InlineData("priority=99")]
    [InlineData("departmentId=0")]
    [InlineData("departmentId=-4")]
    public async Task UnknownFilterValueIsRefusedAsync(string filter)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/tickets/client/1?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SupportTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
