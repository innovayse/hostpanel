namespace Innovayse.Application.Email.Queries.ListEmailDomains;

/// <summary>Returns all email domains that belong to the given client.</summary>
public record ListEmailDomainsQuery(int ClientId);
