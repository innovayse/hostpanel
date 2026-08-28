namespace Innovayse.Application.Clients.Queries.ExportClientData;

using System.Text.Json.Serialization;

/// <summary>
/// The client data export, one property per section the admin export dialog offers.
/// <para>
/// A section the caller did not ask for stays null and every property is annotated
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/>, so an unrequested section is absent from the
/// JSON rather than present and empty. That is the wire contract the admin download already has —
/// the file the browser saves is this payload verbatim — and the property order below is the order
/// the sections appear in it, so it must not be reshuffled.
/// </para>
/// </summary>
public sealed record ClientExportDto
{
    /// <summary>Who the account belongs to. Key <c>profileData</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientExportProfileDto? ProfileData { get; init; }

    /// <summary>Domains registered to the client. Key <c>domains</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportDomainDto>? Domains { get; init; }

    /// <summary>Products and services the client bought. Key <c>productsServices</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportServiceDto>? ProductsServices { get; init; }

    /// <summary>Invoices raised against the client. Key <c>invoices</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportInvoiceDto>? Invoices { get; init; }

    /// <summary>Payments taken from and refunded to the client. Key <c>transactions</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportTransactionDto>? Transactions { get; init; }

    /// <summary>Quotes issued to the client. Key <c>quotes</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportQuoteDto>? Quotes { get; init; }

    /// <summary>Charges queued for the client's next invoice. Key <c>billableItems</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportBillableItemDto>? BillableItems { get; init; }

    /// <summary>Sub-account contacts on the client. Key <c>contacts</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportContactDto>? Contacts { get; init; }

    /// <summary>Support tickets the client opened. Key <c>tickets</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportTicketDto>? Tickets { get; init; }

    /// <summary>Mail sent to the client's address. Key <c>emails</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportEmailDto>? Emails { get; init; }

    /// <summary>Staff notes kept on the account. Key <c>notes</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientExportNotesDto? Notes { get; init; }

    /// <summary>The most recent admin actions on the account. Key <c>activityLog</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClientExportActivityDto>? ActivityLog { get; init; }

    /// <summary>Consent history — not recorded by this platform. Key <c>consentHistory</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientExportNoteDto? ConsentHistory { get; init; }

    /// <summary>Stored payment methods — held by the gateway, not here. Key <c>payMethods</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientExportNoteDto? PayMethods { get; init; }
}
