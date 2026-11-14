using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/tickets</c>: staff can list every support ticket and narrow the list by a subject search and by status,
/// priority, department and flag, all before paging; a plain client is refused: the route carries the class-level
/// <c>Admin</c> policy.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTicketsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can list every support ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListTicketsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/tickets?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A registered client, who is not staff, is forbidden.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientIsForbiddenAsync()
    {
        var (client, _) = await SupportTestSeeds.RegisterClientAsync(factory, "list-tickets-client");

        var response = await client.GetAsync("/api/tickets?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A caller with no credential cannot list tickets.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The staff list names the client who opened each ticket, so it does not have to fall back to a bare client number.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRowsNameTheClientWhoOpenedTheTicketAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        int ticketId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = Client.Create($"user-{Guid.NewGuid():N}", "Grace", "Hopper", $"grace-{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
            context.Clients.Add(client);
            await context.SaveChangesAsync(CancellationToken.None);
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
            var ticket = Ticket.Create(client.Id, "Named requestor", "Hello", departmentId, TicketPriority.Medium, TestMoments.Seeded);
            context.Tickets.Add(ticket);
            await context.SaveChangesAsync(CancellationToken.None);
            ticketId = ticket.Id;
        }

        var response = await admin.GetAsync("/api/tickets?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.GetProperty("items").EnumerateArray().Single(t => t.GetProperty("id").GetInt32() == ticketId);
        row.GetProperty("clientName").GetString().Should().Be("Grace Hopper");
    }

    /// <summary>Seeds one owner with the four-ticket mix the filter tests tell apart.</summary>
    /// <returns>The mix, scoped by its token.</returns>
    private async Task<TicketListMix> SeedMixAsync()
    {
        var owner = await ClientsTestSeeds.SeedClientAsync(factory);
        return await SupportTestSeeds.SeedTicketMixAsync(factory, owner.ClientId);
    }

    /// <summary>The search matches the subject case-insensitively as a substring and leaves other tickets out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheSubjectInAnyLetterCaseAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();

        var (ids, totalCount) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"/api/tickets?page=1&pageSize=100&search=GAMMA-{mix.Token.ToUpperInvariant()}");

        ids.Should().Equal(mix.Gamma);
        totalCount.Should().Be(1);
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

        var (percentIds, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={token}%2050%25");
        var (underscoreIds, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={token}%20a_c");
        var (bareWildcard, bareCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={token}%25%25");

        percentIds.Should().Equal(percent);
        underscoreIds.Should().Equal(underscore);
        bareWildcard.Should().BeEmpty();
        bareCount.Should().Be(0);
    }

    /// <summary>The status filter keeps only the tickets in that status, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatusAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();

        var (open, openCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&status=Open");
        var (closed, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&status=Closed");
        var (awaiting, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&status=AwaitingReply");

        open.Should().Equal(mix.Gamma, mix.Alpha);
        openCount.Should().Be(2);
        closed.Should().Equal(mix.Beta);
        awaiting.Should().Equal(mix.Delta);
    }

    /// <summary>The priority filter keeps only the tickets of that priority.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PriorityFilterKeepsOnlyThatPriorityAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();

        var (high, highCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&priority=High");
        var (low, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&priority=Low");

        high.Should().Equal(mix.Gamma, mix.Beta);
        highCount.Should().Be(2);
        low.Should().Equal(mix.Alpha);
    }

    /// <summary>The department filter keeps only the tickets filed under that department.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DepartmentFilterKeepsOnlyThatDepartmentAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();

        var (one, oneCount) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&departmentId={mix.DepartmentOne}");
        var (two, _) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&departmentId={mix.DepartmentTwo}");

        one.Should().Equal(mix.Beta, mix.Alpha);
        oneCount.Should().Be(2);
        two.Should().Equal(mix.Delta, mix.Gamma);
    }

    /// <summary>The flagged filter keeps only the tickets staff flagged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FlaggedFilterKeepsOnlyFlaggedTicketsAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();

        var (flagged, flaggedCount) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"/api/tickets?page=1&pageSize=100&search={mix.Token}&flagged=true");

        flagged.Should().Equal(mix.Gamma);
        flaggedCount.Should().Be(1);
    }

    /// <summary>The search and every filter narrow together: a ticket must pass each one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CombinedFiltersAllApplyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();
        var scope = $"/api/tickets?page=1&pageSize=100&search={mix.Token}";

        var (openHighTwo, _) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"{scope}&status=Open&priority=High&departmentId={mix.DepartmentTwo}");
        var (closedHigh, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{scope}&status=Closed&priority=High");
        var (closedLow, closedLowCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{scope}&status=Closed&priority=Low");
        var (flaggedClosed, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"{scope}&flagged=true&status=Closed");
        var (searchAndStatus, _) = await SupportTestSeeds.ListTicketIdsAsync(
            admin, $"/api/tickets?page=1&pageSize=100&search=alpha-{mix.Token}&status=Closed");

        openHighTwo.Should().Equal(mix.Gamma);
        closedHigh.Should().Equal(mix.Beta);
        closedLow.Should().BeEmpty();
        closedLowCount.Should().Be(0);
        flaggedClosed.Should().BeEmpty();
        searchAndStatus.Should().BeEmpty();
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePagingAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync();

        var (first, firstCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=3&search={mix.Token}&priority=High");
        var (everything, everythingCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=1&pageSize=3&search={mix.Token}");
        var (second, secondCount) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=2&pageSize=3&search={mix.Token}");
        var (beyond, _) = await SupportTestSeeds.ListTicketIdsAsync(admin, $"/api/tickets?page=3&pageSize=3&search={mix.Token}");

        first.Should().Equal(mix.Gamma, mix.Beta);
        firstCount.Should().Be(2);
        everything.Should().Equal(mix.Delta, mix.Gamma, mix.Beta);
        everythingCount.Should().Be(4);
        second.Should().Equal(mix.Alpha);
        secondCount.Should().Be(4);
        beyond.Should().BeEmpty();
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

        var response = await admin.GetAsync($"/api/tickets?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SupportTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A search longer than the limit is refused with 400, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefusedAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/tickets?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

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

        var response = await admin.GetAsync($"/api/tickets?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SupportTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
