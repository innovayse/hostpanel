namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Interfaces;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Handles <see cref="ExportClientDataQuery"/>: gathers the requested sections of one client's
/// record and returns them as the payload the admin export dialog downloads.
/// <para>
/// Only the sections the caller asked for are read. Exporting an account with a long history is
/// slow enough that the dialog warns about it, so a section nobody ticked costs no query.
/// </para>
/// </summary>
/// <param name="clients">Client repository — the account itself and its contacts.</param>
/// <param name="identity">Reads the person behind the client row, for the sign-in address.</param>
/// <param name="domains">Domain repository.</param>
/// <param name="services">Client service repository.</param>
/// <param name="invoices">Invoice repository.</param>
/// <param name="transactions">Transaction repository.</param>
/// <param name="quotes">Quote repository.</param>
/// <param name="exportReads">Export-specific reads: billable items and the client's mail log.</param>
/// <param name="tickets">Ticket repository.</param>
/// <param name="activityLogs">Activity log repository.</param>
public sealed class ExportClientDataHandler(
    IClientRepository clients,
    IIdentityProvider identity,
    IDomainRepository domains,
    IClientServiceRepository services,
    IInvoiceRepository invoices,
    ITransactionRepository transactions,
    IQuoteRepository quotes,
    IClientExportRepository exportReads,
    ITicketRepository tickets,
    IActivityLogRepository activityLogs)
{
    /// <summary>
    /// Number of activity log entries the export carries, newest first. Capped because the log is
    /// the one section with no natural ceiling — a busy account accumulates thousands of rows, and
    /// a download nobody can open is worse than a truncated one.
    /// </summary>
    private const int ActivityLogLimit = 500;

    /// <summary>
    /// Page size used where a repository only offers a paged read but the export wants everything.
    /// </summary>
    private const int AllRows = int.MaxValue;

    /// <summary>Executes the query.</summary>
    /// <param name="query">The client to export and the sections to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The requested sections of the client's record, or null when no such client exists — the
    /// caller turns that into a 404.
    /// </returns>
    public async Task<ClientExportDto?> HandleAsync(ExportClientDataQuery query, CancellationToken ct)
    {
        var clientId = query.ClientId;

        var client = await clients.FindByIdAsync(clientId, ct);
        if (client is null)
        {
            return null;
        }

        var account = await identity.FindBySubjectAsync(client.UserId, ct);
        var email = account?.Email ?? string.Empty;

        // Case-insensitive because the keys travel on the query string, where a caller may spell
        // them however it likes.
        var wanted = new HashSet<string>(query.Fields, StringComparer.OrdinalIgnoreCase);

        // EF Core's DbContext is not thread-safe, so the sections are read one after another
        // rather than awaited together.
        var profile = wanted.Contains("profileData")
            ? new ClientExportProfileDto(
                client.Id,
                client.FirstName,
                client.LastName,
                email,
                client.CompanyName,
                client.Phone,
                client.Street,
                client.City,
                client.State,
                client.PostCode,
                client.Country,
                client.Status,
                client.CreatedAt)
            : null;

        IReadOnlyList<ClientExportDomainDto>? domainRows = null;
        if (wanted.Contains("domains"))
        {
            var rows = await domains.ListByClientAsync(clientId, ct);
            domainRows = rows
                .Select(d => new ClientExportDomainDto(d.Id, d.Name, d.Status, d.RegisteredAt, d.ExpiresAt))
                .ToList();
        }

        IReadOnlyList<ClientExportServiceDto>? serviceRows = null;
        if (wanted.Contains("productsServices"))
        {
            var rows = await services.ListByClientAsync(clientId, ct);
            serviceRows = rows
                .Select(s => new ClientExportServiceDto(
                    s.Id, s.ProductId, s.BillingCycle, s.Status, s.CreatedAt, s.NextRenewalAt))
                .ToList();
        }

        IReadOnlyList<ClientExportInvoiceDto>? invoiceRows = null;
        if (wanted.Contains("invoices"))
        {
            var rows = await invoices.ListByClientAsync(clientId, ct);
            invoiceRows = rows
                .Select(i => new ClientExportInvoiceDto(i.Id, i.Status, i.Total, i.CreatedAt, i.DueDate, i.PaidAt))
                .ToList();
        }

        IReadOnlyList<ClientExportTransactionDto>? transactionRows = null;
        if (wanted.Contains("transactions"))
        {
            var (rows, _) = await transactions.ListByClientAsync(clientId, 1, AllRows, ct);
            transactionRows = rows
                .Select(t => new ClientExportTransactionDto(
                    t.Id, t.Date, t.Description, t.AmountIn, t.AmountOut, t.Fees, t.PaymentMethod))
                .ToList();
        }

        IReadOnlyList<ClientExportQuoteDto>? quoteRows = null;
        if (wanted.Contains("quotes"))
        {
            var rows = await quotes.ListByClientAsync(clientId, ct);
            quoteRows = rows
                .Select(q => new ClientExportQuoteDto(q.Id, q.Stage, q.Total, q.CreatedAt, q.ExpiryDate))
                .ToList();
        }

        IReadOnlyList<ClientExportBillableItemDto>? billableRows = null;
        if (wanted.Contains("billableItems"))
        {
            billableRows = await exportReads.ListBillableItemsAsync(clientId, ct);
        }

        // Contacts are part of the client aggregate, which the repository already loaded.
        var contactRows = wanted.Contains("contacts")
            ? client.Contacts
                .Select(c => new ClientExportContactDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.Type))
                .ToList()
            : null;

        IReadOnlyList<ClientExportTicketDto>? ticketRows = null;
        if (wanted.Contains("tickets"))
        {
            var rows = await tickets.ListByClientIdAsync(clientId, ct);
            ticketRows = rows
                .Select(t => new ClientExportTicketDto(t.Id, t.Subject, t.Status, t.Priority, t.CreatedAt))
                .ToList();
        }

        IReadOnlyList<ClientExportEmailDto>? emailRows = null;
        if (wanted.Contains("emails"))
        {
            // Keyed on the address the identity provider gave, not on the client id: what ties a
            // log entry to an account is who it was addressed to, and under an external SSO the
            // local user table is not where that address lives.
            emailRows = await exportReads.ListEmailsForRecipientAsync(email, ct);
        }

        IReadOnlyList<ClientExportActivityDto>? activityRows = null;
        if (wanted.Contains("activityLog"))
        {
            var (rows, _) = await activityLogs.ListByClientIdAsync(
                clientId, 1, ActivityLogLimit, null, null, null, null, ct);
            activityRows = rows
                .Select(a => new ClientExportActivityDto(a.Id, a.Description, a.CreatedAt, a.AdminName))
                .ToList();
        }

        return new ClientExportDto
        {
            ProfileData = profile,
            Domains = domainRows,
            ProductsServices = serviceRows,
            Invoices = invoiceRows,
            Transactions = transactionRows,
            Quotes = quoteRows,
            BillableItems = billableRows,
            Contacts = contactRows,
            Tickets = ticketRows,
            Emails = emailRows,
            Notes = wanted.Contains("notes") ? new ClientExportNotesDto(client.AdminNotes) : null,
            ActivityLog = activityRows,
            ConsentHistory = wanted.Contains("consentHistory")
                ? new ClientExportNoteDto("No consent history recorded.")
                : null,
            PayMethods = wanted.Contains("payMethods")
                ? new ClientExportNoteDto("Payment methods are not stored locally.")
                : null,
        };
    }
}
