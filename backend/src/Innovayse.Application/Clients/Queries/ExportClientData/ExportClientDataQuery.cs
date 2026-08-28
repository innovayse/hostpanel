namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>
/// Query for the admin "export client data" download: returns the sections of a client's record
/// that were explicitly asked for, and nothing else.
/// </summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Fields">
/// The section keys to include, matched case-insensitively — <c>profileData</c>, <c>domains</c>,
/// <c>productsServices</c>, <c>invoices</c>, <c>transactions</c>, <c>quotes</c>,
/// <c>billableItems</c>, <c>contacts</c>, <c>tickets</c>, <c>emails</c>, <c>notes</c>,
/// <c>activityLog</c>, <c>consentHistory</c>, <c>payMethods</c>. A key that is absent leaves its
/// section out of the payload entirely rather than emitting it empty.
/// </param>
public sealed record ExportClientDataQuery(int ClientId, IReadOnlyList<string> Fields);
