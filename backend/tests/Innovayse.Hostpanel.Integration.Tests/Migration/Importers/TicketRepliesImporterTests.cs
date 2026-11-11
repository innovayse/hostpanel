using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="TicketRepliesImporter"/>, driven end to end through
/// <c>MigrationPullWorker.RunAsync</c> against a <see cref="FakeMigrationSource"/>: appends a
/// reply onto the open ticket it belongs to, matched by client and subject. A reply the ticket
/// already holds (message, author and staff flag) is skipped as already existing on a retry.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TicketRepliesImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A reply for an existing open ticket is appended.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_AppendsAReplyToAnOpenTicketAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Support = new MigrationSupportTotals(TicketReplies: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.FirstAsync(c => c.UserId == userId);
        var subject = $"ticket-{Guid.NewGuid():N}";
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(client.Id, subject, "Initial message", departmentId, TicketPriority.Medium, SeededAt);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.TicketReplies);
        source.SetDatasetJson("ticket_replies", $$"""
            [{"ClientEmail":"{{email}}","TicketSubject":"{{subject}}","Message":"Reply body","AuthorName":"Agent","IsStaffReply":true}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var reloaded = await context.Tickets.AsNoTracking().Include(t => t.Replies).FirstAsync(t => t.Id == ticket.Id);
        Assert.Contains(reloaded.Replies, r => r.Message == "Reply body");
    }

    /// <summary>A seeded open ticket together with the source email and subject that address it.</summary>
    /// <param name="Email">The client email the reply records carry.</param>
    /// <param name="Subject">The ticket subject the reply records carry.</param>
    /// <param name="TicketId">The seeded ticket's id.</param>
    private sealed record SeededTicket(string Email, string Subject, int TicketId);

    /// <summary>Seeds a client and one open ticket for it.</summary>
    /// <param name="scope">The scope whose context seeds the rows.</param>
    /// <returns>The seeded ticket and the keys that address it.</returns>
    private static async Task<SeededTicket> SeedTicketAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.FirstAsync(c => c.UserId == userId);
        var subject = $"ticket-{Guid.NewGuid():N}";
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(client.Id, subject, "Initial message", departmentId, TicketPriority.Medium, SeededAt);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);
        return new SeededTicket(email, subject, ticket.Id);
    }

    /// <summary>Builds a source holding the given ticket replies dataset.</summary>
    /// <param name="json">The dataset's single page as a JSON array.</param>
    /// <returns>The seeded fake source.</returns>
    private static FakeMigrationSource SourceWith(string json)
    {
        var source = new FakeMigrationSource { Totals = MigrationSourceTotals.Empty with { Support = new MigrationSupportTotals(TicketReplies: 1) } };
        source.SetDatasetJson("ticket_replies", json);
        return source;
    }

    /// <summary>Builds one reply record as JSON.</summary>
    /// <param name="ticket">The ticket the record addresses.</param>
    /// <param name="message">The reply message, or <see langword="null"/> for a blank one.</param>
    /// <param name="author">The reply author, or <see langword="null"/> for a blank one.</param>
    /// <param name="isStaff">Whether the reply is a staff reply.</param>
    /// <returns>The record as a JSON object.</returns>
    private static string ReplyJson(SeededTicket ticket, string? message, string? author, bool isStaff)
    {
        var messageJson = message is null ? "null" : $"\"{message}\"";
        var authorJson = author is null ? "null" : $"\"{author}\"";
        return $$"""{"ClientEmail":"{{ticket.Email}}","TicketSubject":"{{ticket.Subject}}","Message":{{messageJson}},"AuthorName":{{authorJson}},"IsStaffReply":{{(isStaff ? "true" : "false")}}}""";
    }

    /// <summary>Runs one pull of the ticket replies dataset as a fresh job and returns the reloaded job.</summary>
    /// <param name="scope">The scope whose context and worker run the pull.</param>
    /// <returns>The job after the pull finished.</returns>
    private static async Task<Domain.Migration.MigrationJob> PullAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.TicketReplies);
        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);
        return await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
    }

    /// <summary>Counts the replies on a ticket.</summary>
    /// <param name="scope">The scope whose context reads the ticket.</param>
    /// <param name="ticketId">The ticket to count replies on.</param>
    /// <returns>The number of replies stored on the ticket.</returns>
    private static async Task<int> ReplyCountAsync(IServiceScope scope, int ticketId)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var ticket = await context.Tickets.AsNoTracking().Include(t => t.Replies).SingleAsync(t => t.Id == ticketId);
        return ticket.Replies.Count;
    }

    /// <summary>A second run adds no second reply: the retry imports 0 and skips the record as already existing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_SecondRunSkipsAsAlreadyExistsAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, "Reply body", "Agent", true)}]");

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await ReplyCountAsync(scope, ticket.TicketId));
        Assert.Equal(0, second.TicketRepliesImported);
        Assert.Equal(1, second.TicketRepliesSkipped);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == second.Id && l.EntityType == MigrationEntityType.TicketReplies);
        Assert.Equal(MigrationSkipReason.AlreadyExists, log.SkipReason);
    }

    /// <summary>Two different replies on one ticket are both imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_DifferentRepliesAreBothImportedAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, "First reply", "Agent", true)},{ReplyJson(ticket, "Second reply", "Agent", true)}]");

        var job = await PullAsync(scope);

        Assert.Equal(2, await ReplyCountAsync(scope, ticket.TicketId));
        Assert.Equal(2, job.TicketRepliesImported);
    }

    /// <summary>The same message from a different author is a different reply and is imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_SameMessageFromAnotherAuthorIsImportedAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, "Same text", "Agent", true)},{ReplyJson(ticket, "Same text", "Other agent", true)}]");

        var job = await PullAsync(scope);

        Assert.Equal(2, await ReplyCountAsync(scope, ticket.TicketId));
        Assert.Equal(2, job.TicketRepliesImported);
    }

    /// <summary>The same person writing the same words twice in one run gets both replies, since only replies older than the job count as duplicates.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_IdenticalRepliesInOneRunAreBothImportedAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, "OK", "Agent", true)},{ReplyJson(ticket, "OK", "Agent", true)}]");

        var job = await PullAsync(scope);

        Assert.Equal(2, await ReplyCountAsync(scope, ticket.TicketId));
        Assert.Equal(2, job.TicketRepliesImported);
    }

    /// <summary>A retry of a run that imported two identical replies skips both and adds none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_RetrySkipsIdenticalRepliesImportedEarlierAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, "OK", "Agent", true)},{ReplyJson(ticket, "OK", "Agent", true)}]");
        await PullAsync(scope);

        var second = await PullAsync(scope);

        Assert.Equal(2, await ReplyCountAsync(scope, ticket.TicketId));
        Assert.Equal(0, second.TicketRepliesImported);
        Assert.Equal(2, second.TicketRepliesSkipped);
    }

    /// <summary>A ticket closed between two runs still reports the earlier reply as already existing, not as a closed ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_ClosedBetweenRunsReportsAlreadyExistsAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, "Reply body", "Agent", true)}]");
        await PullAsync(scope);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var tracked = await context.Tickets.SingleAsync(t => t.Id == ticket.TicketId);
        tracked.Close(SeededAt);
        await context.SaveChangesAsync(CancellationToken.None);

        var second = await PullAsync(scope);

        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == second.Id && l.EntityType == MigrationEntityType.TicketReplies);
        Assert.Equal(MigrationSkipReason.AlreadyExists, log.SkipReason);
        Assert.Equal(1, await ReplyCountAsync(scope, ticket.TicketId));
    }

    /// <summary>A blank message and author are normalised to the shared placeholders, so the retry matches the stored form.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_TicketReplies_BlankValuesMatchTheirNormalisedFormAsync()
    {
        var source = SourceWith("[]");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var ticket = await SeedTicketAsync(scope);
        source.SetDatasetJson("ticket_replies", $"[{ReplyJson(ticket, null, null, false)}]");

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await ReplyCountAsync(scope, ticket.TicketId));
        Assert.Equal(0, second.TicketRepliesImported);
        Assert.Equal(1, second.TicketRepliesSkipped);
    }
}
