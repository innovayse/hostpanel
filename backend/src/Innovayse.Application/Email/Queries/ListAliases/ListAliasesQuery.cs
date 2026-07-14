namespace Innovayse.Application.Email.Queries.ListAliases;

/// <summary>
/// Returns all mail aliases belonging to the specified email domain.
/// </summary>
public record ListAliasesQuery(int EmailDomainId);
