namespace Innovayse.Hostpanel.Application.Clients.Queries.FindClientIds;

/// <summary>
/// Asks which client records a person typed a search for: another module's list that shows a client per row (an invoice,
/// a quote, a transaction) keeps the rows of these clients next to its own text matches, because only the Clients module
/// and the identity provider hold the name and the sign-in address.
/// </summary>
/// <param name="Search">
/// The text to look for (case-insensitive, a <c>%</c> or <c>_</c> matches itself) in the client's first name, last name,
/// full name, company, sign-in address and account name.
/// </param>
public sealed record FindClientIdsQuery(string Search);
