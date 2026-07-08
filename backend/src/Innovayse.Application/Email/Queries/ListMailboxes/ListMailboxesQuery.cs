namespace Innovayse.Application.Email.Queries.ListMailboxes;

/// <summary>
/// Returns all mailboxes belonging to the specified email domain.
/// </summary>
public record ListMailboxesQuery(int EmailDomainId);
