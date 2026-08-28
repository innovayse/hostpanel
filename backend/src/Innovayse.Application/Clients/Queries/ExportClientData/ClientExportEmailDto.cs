namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>
/// One outbound email, as the <c>emails</c> section of a client data export lists it. Only the
/// envelope is exported — the rendered body is deliberately left out of the download.
/// </summary>
/// <param name="Id">The email log entry's primary key.</param>
/// <param name="Subject">Subject line the recipient saw.</param>
/// <param name="SentAt">When delivery was attempted.</param>
/// <param name="Success">Whether the send succeeded.</param>
public sealed record ClientExportEmailDto(
    int Id,
    string Subject,
    DateTimeOffset SentAt,
    bool Success);
