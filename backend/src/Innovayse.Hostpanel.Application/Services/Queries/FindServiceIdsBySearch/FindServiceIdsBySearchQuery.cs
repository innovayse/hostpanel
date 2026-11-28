namespace Innovayse.Hostpanel.Application.Services.Queries.FindServiceIdsBySearch;

/// <summary>
/// Asks which client services a search text names, so a list of the Services module (the services, their cancellation
/// requests) searches by what a service shows without each rebuilding the match.
/// </summary>
/// <param name="Search">
/// The text to look for, in any letter case, in the service's own domain, its linked registered domain, its product
/// name and its client (name, company, sign-in email), or the service id when it is a whole number; a blank text
/// matches nothing.
/// </param>
/// <param name="ClientId">
/// The client the list is scoped to, or <see langword="null"/> for every client; a scoped list holds only that
/// client's services, so the client's name is not searched.
/// </param>
public sealed record FindServiceIdsBySearchQuery(string Search, int? ClientId = null);
