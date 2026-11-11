using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Audit.Queries.ListClientActivityLogs;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Audit;

/// <summary>
/// <see cref="ListClientActivityLogsQuery"/>, both through the real <see cref="IMessageBus"/> (the
/// date window, the admin/description/IP substring filters, and the newest-first order)
/// and through
/// <c>ClientActivityLogsController</c>'s <c>GET /api/clients/{id}/activity-logs</c> (the
/// authorization boundary and the paging shape). This query is the module's only use case.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientActivityLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A client id no seeded or created client ever takes.</summary>
    private const int NoSuchClientId = 999_999_999;

    /// <summary>
    /// Adds a log entry for <paramref name="clientId"/> and back-dates <c>CreatedAt</c> directly on
    /// the tracked entry: <see cref="ActivityLog.Create"/> always stamps <see cref="DateTimeOffset.UtcNow"/>
    /// and takes no timestamp argument, so a specific date can only be written this way.
    /// </summary>
    /// <param name="context">The data context to seed into.</param>
    /// <param name="clientId">FK the row is filed under.</param>
    /// <param name="description">The entry's description.</param>
    /// <param name="createdAt">The timestamp to force onto the row after insertion.</param>
    /// <param name="adminName">Optional admin display name.</param>
    /// <param name="adminEmail">Optional admin email.</param>
    /// <param name="ipAddress">Optional IP address.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    private static async Task SeedLogAsync(
        IHostpanelDbContext context, int clientId, string description, DateTimeOffset createdAt,
        string? adminName = null, string? adminEmail = null, string? ipAddress = null)
    {
        var log = ActivityLog.Create(clientId, description, adminId: null, adminName, adminEmail, ipAddress);
        context.ActivityLogs.Add(log);
        await context.SaveChangesAsync(CancellationToken.None);
        ((Microsoft.EntityFrameworkCore.DbContext)context).Entry(log).Property(l => l.CreatedAt).CurrentValue = createdAt;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Results come back newest first, so the latest activity leads.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_OrdersNewestFirstAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var now = SeededAt;
        await SeedLogAsync(context, clientId, "Oldest", now.AddDays(-2));
        await SeedLogAsync(context, clientId, "Newest", now);
        await SeedLogAsync(context, clientId, "Middle", now.AddDays(-1));

        var result = await bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            new ListClientActivityLogsQuery(clientId, 1, 10, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Newest", "Middle", "Oldest"], result.Value!.Items.Select(i => i.Description));
    }

    /// <summary>The date filter matches only entries created on that UTC calendar day.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_DateFilter_MatchesOnlyThatUtcDayAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var targetDay = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);
        await SeedLogAsync(context, clientId, "OnTargetDay", targetDay);
        await SeedLogAsync(context, clientId, "DayBefore", targetDay.AddDays(-1));
        await SeedLogAsync(context, clientId, "DayAfter", targetDay.AddDays(1));

        var result = await bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            new ListClientActivityLogsQuery(clientId, 1, 10, targetDay, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(["OnTargetDay"], result.Value!.Items.Select(i => i.Description));
    }

    /// <summary>The admin search matches a substring of either the admin's name or their email.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_AdminSearch_MatchesNameOrEmailSubstringAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var now = SeededAt;
        await SeedLogAsync(context, clientId, "ByName", now, adminName: "Ada Lovelace");
        await SeedLogAsync(context, clientId, "ByEmail", now.AddMinutes(-1), adminEmail: "ada@example.test");
        await SeedLogAsync(context, clientId, "NoMatch", now.AddMinutes(-2), adminName: "Grace Hopper");

        var result = await bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            new ListClientActivityLogsQuery(clientId, 1, 10, null, "ada", null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(["ByEmail", "ByName"], result.Value!.Items.Select(i => i.Description).OrderBy(d => d));
    }

    /// <summary>The description filter matches a case-insensitive substring.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_DescriptionFilter_MatchesCaseInsensitiveSubstringAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var now = SeededAt;
        await SeedLogAsync(context, clientId, "Added Invoice Payment", now);
        await SeedLogAsync(context, clientId, "Refunded Invoice", now.AddMinutes(-1));

        var result = await bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            new ListClientActivityLogsQuery(clientId, 1, 10, null, null, "PAYMENT", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Added Invoice Payment"], result.Value!.Items.Select(i => i.Description));
    }

    /// <summary>The IP filter matches a substring of the recorded address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_IpAddressFilter_MatchesSubstringAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var now = SeededAt;
        await SeedLogAsync(context, clientId, "FromOffice", now, ipAddress: "10.0.0.5");
        await SeedLogAsync(context, clientId, "FromHome", now.AddMinutes(-1), ipAddress: "192.168.1.1");

        var result = await bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            new ListClientActivityLogsQuery(clientId, 1, 10, null, null, null, "10.0.0"));

        Assert.True(result.IsSuccess);
        Assert.Equal(["FromOffice"], result.Value!.Items.Select(i => i.Description));
    }

    /// <summary>An entry belonging to a different client is never returned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NeverReturnsAnotherClientsEntryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        // A real second client: the log's client is a foreign key, so another client's entry needs one.
        var otherClientId = await DomainsTestSeeds.SeedClientAsync(context);
        var now = SeededAt;
        await SeedLogAsync(context, clientId, "Mine", now);
        await SeedLogAsync(context, otherClientId, "TheirsNotMine", now);

        var result = await bus.InvokeAsync<Result<PaginatedList<ActivityLogDto>>>(
            new ListClientActivityLogsQuery(clientId, 1, 10, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Mine"], result.Value!.Items.Select(i => i.Description));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymousAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients/1/activity-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client with no recorded activity answers 200 with an empty page.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ClientWithNoActivity_ReturnsAnEmptyPageAsync()
    {
        var admin = await AdminClientAsync();
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/activity-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>An id that names no client simply answers an empty page, not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownClientId_ReturnsAnEmptyPageAsync()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/clients/{NoSuchClientId}/activity-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    /// <summary>A search filter past the length limit is refused before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_OverlongFilter_IsRefusedAsync()
    {
        var admin = await AdminClientAsync();
        var filter = new string('a', 201);

        var response = await admin.GetAsync($"/api/clients/{NoSuchClientId}/activity-logs?description={filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
