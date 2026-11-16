namespace Innovayse.Hostpanel.Application.Domains.Queries.FindServiceIdsByDomainName;

/// <summary>
/// Asks which client services have a registered domain linked whose name contains the text, so the Services module
/// can search its list by the domain a service shows without reading the Domains table itself.
/// </summary>
/// <param name="Search">The text to look for in the domain names, in any letter case; a blank text matches nothing.</param>
public sealed record FindServiceIdsByDomainNameQuery(string Search);
