using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Enums;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports ticket reply records onto the ticket they belong to, matched by client and subject.
/// A retried or repeated run is duplicate-safe: a reply whose message, author and staff flag the
/// ticket already holds is skipped with <see cref="MigrationSkipReason.AlreadyExists"/>.
/// </summary>
public sealed class TicketRepliesImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Dates an imported reply.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="TicketRepliesImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="clock">Dates an imported reply.</param>
    public TicketRepliesImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver,
        IClock clock)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
        _clock = clock;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.TicketReplies;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<TicketReplyRecord>(job, EntityType, "ticket_replies", ImportReplyAsync), ct);

    /// <summary>Imports one plugin-side ticket reply record.</summary>
    /// <param name="rec">The ticket reply record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the ticket reply was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportReplyAsync(TicketReplyRecord rec, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        // Tracked on purpose: AddReply below mutates the ticket and SaveChanges must see the
        // new reply. An untracked ticket reported "Imported" and wrote nothing.
        var tickets = await _context.Tickets
            .Include(t => t.Replies).Include(t => t.Tags)
            .Where(t => t.ClientId == clientId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var ticket = tickets.FirstOrDefault(t =>
            string.Equals(t.Subject, rec.TicketSubject, StringComparison.OrdinalIgnoreCase));

        if (ticket is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.TicketNotFound);
        }

        // Normalised exactly as on write, so a blank value matches the placeholder it was stored as.
        var message = string.IsNullOrWhiteSpace(rec.Message) ? "(no message)" : rec.Message;
        // TicketReply.AuthorName is NOT NULL and defaults to empty, so a missing author is stored as the
        // empty string: a neutral value, never display text. The frontend renders its own fallback.
        var authorName = string.IsNullOrWhiteSpace(rec.AuthorName) ? string.Empty : rec.AuthorName;

        // Checked before the closed-ticket rule: a reply imported earlier stays "already exists"
        // even when the ticket was closed since.
        if (ticket.HasReply(message, authorName, rec.IsStaffReply))
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.TicketClosed);
        }

        ticket.AddReply(message, authorName, rec.IsStaffReply, _clock.UtcNow);

        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
