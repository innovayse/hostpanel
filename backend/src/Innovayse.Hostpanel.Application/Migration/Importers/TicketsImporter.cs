using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports support ticket records, deduplicated by client and subject, matched against this
/// install's own departments by name.
/// </summary>
public sealed class TicketsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>tickets</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Dates the opening and closing of an imported ticket.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="TicketsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>tickets</c> dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="clock">Dates the opening and closing of an imported ticket.</param>
    public TicketsImporter(
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
    public MigrationEntityType EntityType => MigrationEntityType.Tickets;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        // Read-only, so no tracking is needed.
        var departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var defaultDeptId = departments.FirstOrDefault()?.Id;

        await _runner.RunAsync(
            new MigrationRecordRun<TicketRecord>(
                job,
                EntityType,
                "tickets",
                (rec, token) =>
                {
                    var deptId = departments.FirstOrDefault(d =>
                                     string.Equals(d.Name, rec.DepartmentName, StringComparison.OrdinalIgnoreCase))
                                 ?.Id ?? defaultDeptId;
                    return ImportTicketAsync(rec, deptId, token);
                }),
            ct);
    }

    /// <summary>Imports one plugin-side ticket record into the resolved department.</summary>
    /// <param name="rec">The ticket record read from the source plugin.</param>
    /// <param name="departmentId">The local department id resolved for this record, or null when no department exists here.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the ticket was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportTicketAsync(TicketRecord rec, int? departmentId, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        if (departmentId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.DepartmentNotFound);
        }

        var existingTicket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.ClientId == clientId.Value && t.Subject.ToLower() == rec.Subject.ToLower(), ct);
        if (existingTicket is not null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        var priority = rec.Priority switch
        {
            SourceTicketPriorities.High => TicketPriority.High,
            SourceTicketPriorities.Low => TicketPriority.Low,
            _ => TicketPriority.Medium,
        };

        var now = _clock.UtcNow;
        var ticket = Ticket.Create(
            clientId.Value,
            rec.Subject,
            string.IsNullOrWhiteSpace(rec.Message) ? MigrationPlaceholders.NoMessage : rec.Message,
            departmentId.Value,
            priority,
            now);

        if (rec.Status is SourceTicketStatuses.Closed or SourceTicketStatuses.Answered)
        {
            ticket.Close(now);
        }

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
