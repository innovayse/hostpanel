using System.Globalization;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.ExportClientData;

/// <summary>
/// Gathers the requested sections of one client's record and returns them as the payload the
/// admin export dialog downloads.
/// <para>
/// Only the sections the caller asked for are read. Exporting an account with a long history is
/// slow enough that the dialog warns about it, so a section nobody ticked costs no query.
/// </para>
/// </summary>
public sealed class ExportClientDataHandler
{
    /// <summary>
    /// Number of rows the <c>activityLog</c> and <c>emails</c> sections carry, newest first. Capped
    /// because those two are the sections with no natural ceiling; the payload says when it cut them.
    /// </summary>
    private const int LongListLimit = 500;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the people behind the client row and its linked users, for their sign-in addresses.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Records the activity log line for this export, saved by the handler once the sections are read.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the people behind the client row and its linked users, for their sign-in addresses.</param>
    /// <param name="activity">Records the activity log line for this export, saved by the handler once the sections are read.</param>
    public ExportClientDataHandler(IHostpanelDbContext context, IIdentityProvider identity, IActivityLogger activity)
    {
        _context = context;
        _identity = identity;
        _activity = activity;
    }

    /// <summary>Executes the command: reads the asked sections and records the export in the client's log.</summary>
    /// <param name="command">The client to export and the sections to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The requested sections of the client's record, or <c>CLIENT_NOT_FOUND</c> when no such
    /// client exists.
    /// </returns>
    public async Task<Result<ClientExportDto>> HandleAsync(ExportClientDataCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var clientId = command.ClientId;

        var client = await _context.Clients
            .Include(c => c.Contacts)
            .Include(c => c.Users)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null)
        {
            return Result<ClientExportDto>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        // Whether any payment module holds a customer for this client: the saved-card flag of the export.
        var hasSavedCard = await _context.GatewayCustomers.AsNoTracking().AnyAsync(g => g.ClientId == clientId, ct);

        var account = await _identity.FindBySubjectAsync(client.UserId, ct);
        var email = account?.Email ?? string.Empty;

        // The validator has already refused any key that names no section; the match stays
        // case-insensitive because the keys travel on the query string, where a caller may spell
        // them however it likes.
        var wanted = new HashSet<string>(command.Fields, StringComparer.OrdinalIgnoreCase);

        // EF Core's DbContext is not thread-safe, so the sections are read one after another.
        var dto = new ClientExportDto
        {
            ProfileData = wanted.Contains(ClientExportSections.ProfileData) ? MapProfile(client, email) : null,
            Domains = wanted.Contains(ClientExportSections.Domains) ? await ReadDomainsAsync(clientId, ct) : null,
            ProductsServices = wanted.Contains(ClientExportSections.ProductsServices) ? await ReadServicesAsync(clientId, ct) : null,
            Invoices = wanted.Contains(ClientExportSections.Invoices) ? await ReadInvoicesAsync(clientId, ct) : null,
            Transactions = wanted.Contains(ClientExportSections.Transactions) ? await ReadTransactionsAsync(clientId, ct) : null,
            Quotes = wanted.Contains(ClientExportSections.Quotes) ? await ReadQuotesAsync(clientId, ct) : null,
            BillableItems = wanted.Contains(ClientExportSections.BillableItems) ? await ReadBillableItemsAsync(clientId, ct) : null,

            // Contacts and linked users are part of the client aggregate, already loaded above.
            Contacts = wanted.Contains(ClientExportSections.Contacts) ? client.Contacts.Select(MapContact).ToList() : null,
            Users = wanted.Contains(ClientExportSections.Users) ? await ReadUsersAsync(client, ct) : null,
            Invitations = wanted.Contains(ClientExportSections.Invitations) ? await ReadInvitationsAsync(clientId, ct) : null,
            Tickets = wanted.Contains(ClientExportSections.Tickets) ? await ReadTicketsAsync(clientId, ct) : null,
            Emails = wanted.Contains(ClientExportSections.Emails) ? await ReadEmailsAsync(client, email, ct) : null,
            Notes = wanted.Contains(ClientExportSections.Notes) ? new ClientExportNotesDto(client.AdminNotes) : null,
            ActivityLog = wanted.Contains(ClientExportSections.ActivityLog) ? await ReadActivityLogAsync(clientId, ct) : null,

            // Only the current marketing choice is stored, so that is the whole history there is.
            ConsentHistory = wanted.Contains(ClientExportSections.ConsentHistory)
                ? new[] { new ClientExportConsentDto(ConsentPurpose.Marketing, client.MarketingOptIn) }
                : null,

            // Card details live at the card gateway; what is held here is the preference and whether a payment module keeps a customer for the client.
            PayMethods = wanted.Contains(ClientExportSections.PayMethods)
                ? MapPayMethods(client, hasSavedCard)
                : null,
        };

        // An export is a bulk read of personal data, so it leaves a line in the client's log naming
        // who took it (stamped from the caller) and which sections, never what they held. Written
        // once the sections have been read: a failed read exports nothing and logs nothing.
        var sections = string.Join(", ", wanted.Order(StringComparer.OrdinalIgnoreCase));
        await _activity.RecordAsync(
            new ActivityLogEntry(clientId, $"Exported Client Data - Client ID: {clientId} - Sections: {sections}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result<ClientExportDto>.Success(dto);
    }

    /// <summary>Maps the client row to the <c>profileData</c> section.</summary>
    /// <param name="client">The client being exported.</param>
    /// <param name="email">The sign-in address, empty when the identity store no longer knows the owner.</param>
    /// <returns>The profile section.</returns>
    private static ClientExportProfileDto MapProfile(Client client, string email) => new(
        client.Id, client.FirstName, client.LastName, email, client.CompanyName, client.Phone,
        client.ToAddress(),
        client.Status,
        new ClientExportBillingDto(client.Currency, client.CreditBalance, client.PaymentMethod, client.BillingContact),
        client.MarketingOptIn,
        new ClientExportNotificationsDto(
            client.NotifyGeneral, client.NotifyInvoice, client.NotifySupport,
            client.NotifyProduct, client.NotifyDomain, client.NotifyAffiliate),
        new ClientExportSettingsDto(
            client.OverdueNotices, client.TaxExempt, client.SeparateInvoices,
            client.DisableCcProcessing, client.StatusUpdate, client.AllowSso),
        new RegistrationInfo(
            client.RegistrationIp, client.RegistrationUserAgent, client.DeviceType, client.OperatingSystem, client.Browser),
        client.CreatedAt);

    /// <summary>Maps one contact to its <c>contacts</c> row.</summary>
    /// <param name="contact">One of the client's contacts.</param>
    /// <returns>The contact row.</returns>
    private static ClientExportContactDto MapContact(Contact contact) => new(
        contact.Id, contact.FirstName, contact.LastName, contact.CompanyName, contact.Email, contact.Phone, contact.Type,
        contact.Street, contact.Address2, contact.City, contact.State, contact.PostCode, contact.Country,
        new ClientExportNotificationsDto(
            contact.NotifyGeneral, contact.NotifyInvoice, contact.NotifySupport,
            contact.NotifyProduct, contact.NotifyDomain, contact.NotifyAffiliate),
        contact.CreatedAt);

    /// <summary>Builds the <c>payMethods</c> section.</summary>
    /// <param name="client">The client being exported.</param>
    /// <param name="hasSavedCard">Whether a payment module keeps a customer for the client.</param>
    /// <returns>One entry when the account has a payment preference or a gateway customer, none otherwise.</returns>
    private static IReadOnlyList<ClientExportPayMethodDto> MapPayMethods(Client client, bool hasSavedCard)
    {
        if (client.PaymentMethod is null && !hasSavedCard)
        {
            return [];
        }

        return [new ClientExportPayMethodDto(client.PaymentMethod, hasSavedCard)];
    }

    /// <summary>Reads the client's domains.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>domains</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportDomainDto>> ReadDomainsAsync(int clientId, CancellationToken ct) =>
        await _context.Domains.AsNoTracking()
            .Where(d => d.ClientId == clientId)
            .OrderBy(d => d.Id)
            .Select(d => new ClientExportDomainDto(d.Id, d.Name, d.Status, d.RegisteredAt, d.ExpiresAt))
            .ToListAsync(ct);

    /// <summary>Reads the client's services.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>productsServices</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportServiceDto>> ReadServicesAsync(int clientId, CancellationToken ct) =>
        await _context.ClientServices.AsNoTracking()
            .Where(s => s.ClientId == clientId)
            .OrderBy(s => s.Id)
            .Select(s => new ClientExportServiceDto(s.Id, s.ProductId, s.BillingCycle, s.Status, s.CreatedAt, s.NextRenewalAt))
            .ToListAsync(ct);

    /// <summary>Reads the client's invoices.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>invoices</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportInvoiceDto>> ReadInvoicesAsync(int clientId, CancellationToken ct) =>
        await _context.Invoices.AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .OrderBy(i => i.Id)
            .Select(i => new ClientExportInvoiceDto(i.Id, i.Status, i.Total, i.CreatedAt, i.DueDate, i.PaidAt, i.Currency))
            .ToListAsync(ct);

    /// <summary>Reads the client's ledger transactions, newest first.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>transactions</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportTransactionDto>> ReadTransactionsAsync(int clientId, CancellationToken ct) =>
        await _context.Transactions.AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Select(t => new ClientExportTransactionDto(t.Id, t.Date, t.Description, t.AmountIn, t.AmountOut, t.Fees, t.PaymentMethod))
            .ToListAsync(ct);

    /// <summary>Reads the client's quotes.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>quotes</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportQuoteDto>> ReadQuotesAsync(int clientId, CancellationToken ct) =>
        await _context.Quotes.AsNoTracking()
            .Where(q => q.ClientId == clientId)
            .OrderBy(q => q.Id)
            .Select(q => new ClientExportQuoteDto(q.Id, q.Stage, q.Total, q.CreatedAt, q.ExpiryDate))
            .ToListAsync(ct);

    /// <summary>
    /// Reads every billable item on the account whatever its state: invoiced, uninvoiced or
    /// recurring alike, unlike the other repositories that only ever answer one slice.
    /// </summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>billableItems</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportBillableItemDto>> ReadBillableItemsAsync(int clientId, CancellationToken ct) =>
        await _context.BillableItems.AsNoTracking()
            .Where(b => b.ClientId == clientId)
            .OrderBy(b => b.Id)
            .Select(b => new ClientExportBillableItemDto(b.Id, b.Description, b.Amount, b.NextDueDate))
            .ToListAsync(ct);

    /// <summary>Reads the client's support tickets.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>tickets</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportTicketDto>> ReadTicketsAsync(int clientId, CancellationToken ct) =>
        await _context.Tickets.AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .OrderBy(t => t.Id)
            .Select(t => new ClientExportTicketDto(t.Id, t.Subject, t.Status, t.Priority, t.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Reads the people linked to the account, with the sign-in address of each.</summary>
    /// <param name="client">The client, with its linked users loaded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>users</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportUserDto>> ReadUsersAsync(Client client, CancellationToken ct)
    {
        var accounts = await _identity.GetAccountsBySubjectsAsync(client.Users.Select(u => u.UserId).Distinct(), ct);

        return client.Users
            .OrderBy(u => u.Id)
            .Select(u => new ClientExportUserDto(
                u.UserId,
                accounts.TryGetValue(u.UserId, out var linked) ? linked.Email : string.Empty,
                u.Permissions,
                u.CreatedAt))
            .ToList();
    }

    /// <summary>Reads the invitations sent for the account. The acceptance token is never exported.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>invitations</c> rows.</returns>
    private async Task<IReadOnlyList<ClientExportInvitationDto>> ReadInvitationsAsync(int clientId, CancellationToken ct) =>
        await _context.Invitations.AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .OrderBy(i => i.Id)
            .Select(i => new ClientExportInvitationDto(
                i.Id, i.Email, i.FirstName, i.LastName, i.Permissions, i.CreatedAt, i.ExpiresAt, i.AcceptedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Reads the mail sent to the account: its sign-in address and every contact's address. Matched on
    /// the address rather than the client id because that is what ties a log entry to an account, and
    /// matched case-insensitively because the relay and the person typing the address may disagree
    /// about case.
    /// </summary>
    /// <param name="client">The client, with its contacts loaded.</param>
    /// <param name="email">The sign-in address, empty when the identity store no longer knows the owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newest <see cref="LongListLimit"/> rows and whether older ones were left out.</returns>
    private async Task<ClientExportLimitedDto<ClientExportEmailDto>> ReadEmailsAsync(Client client, string email, CancellationToken ct)
    {
        var addresses = client.Contacts.Select(c => c.Email)
            .Append(email)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.ToLower(CultureInfo.InvariantCulture))
            .Distinct()
            .ToList();

        // One row more than the limit is read, so "there is more" is known without a second count query.
        var rows = await _context.EmailLogs.AsNoTracking()
            .Where(e => addresses.Contains(e.To.ToLower()))
            .OrderByDescending(e => e.SentAt)
            .ThenByDescending(e => e.Id)
            .Take(LongListLimit + 1)
            .Select(e => new ClientExportEmailDto(e.Id, e.Subject, e.SentAt, e.Success))
            .ToListAsync(ct);

        return Limited(rows);
    }

    /// <summary>Reads the newest admin actions on the account.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newest <see cref="LongListLimit"/> rows and whether older ones were left out.</returns>
    private async Task<ClientExportLimitedDto<ClientExportActivityDto>> ReadActivityLogAsync(int clientId, CancellationToken ct)
    {
        var rows = await _context.ActivityLogs.AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(LongListLimit + 1)
            .Select(a => new ClientExportActivityDto(a.Id, a.Description, a.CreatedAt, a.AdminName))
            .ToListAsync(ct);

        return Limited(rows);
    }

    /// <summary>Wraps rows read with one row more than the limit, dropping that extra row and flagging the cut.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="rows">The rows, at most <see cref="LongListLimit"/> plus one.</param>
    /// <returns>The section carrying at most the limit and whether the extra row existed.</returns>
    private static ClientExportLimitedDto<T> Limited<T>(List<T> rows) =>
        new(rows.Take(LongListLimit).ToList(), rows.Count > LongListLimit, LongListLimit);
}
