namespace Innovayse.Hostpanel.Application.Billing.Queries.ListMyCreditNotes;

/// <summary>Query for the credit notes issued to the calling client.</summary>
/// <remarks>
/// Carries no client id. Which account is resolved inside the handler from the credential.
/// </remarks>
public sealed record ListMyCreditNotesQuery();
