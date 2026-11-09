using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports client contact records onto the client they belong to, deduplicated by email.
/// </summary>
public sealed class ContactsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Initializes a new instance of the <see cref="ContactsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    public ContactsImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Contacts;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<ContactRecord>(job, EntityType, "contacts", ImportContactAsync), ct);

    /// <summary>Imports one plugin-side contact record onto the client it belongs to.</summary>
    /// <param name="rec">The contact record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the contact was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportContactAsync(ContactRecord rec, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        var client2 = await _context.Clients
            .IncludeChildren()
            .FirstOrDefaultAsync(c => c.Id == clientId.Value, ct);
        if (client2 is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        if (client2.Contacts.Any(c => string.Equals(c.Email, rec.Email, StringComparison.OrdinalIgnoreCase)))
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        // ImportContact, not AddContact: a client closed in the source keeps the contacts it had,
        // and importing history is not a change the closed guard is there to stop.
        client2.ImportContact(new ContactDetails(
            rec.FirstName, rec.LastName, rec.Company,
            rec.Email, rec.Phone, ContactType.General,
            new PostalAddress(rec.Address, null, rec.City, rec.State, rec.PostCode, rec.Country),
            new ContactNotifications(true, true, true, true, true, false)));

        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
