namespace Innovayse.Application.Notifications.Queries.GetClientEmailLog;

/// <summary>Query for one email a client was sent, including its rendered body.</summary>
/// <param name="ClientId">The client's primary key. Never taken from the request.</param>
/// <param name="EmailLogId">The log entry's primary key.</param>
public record GetClientEmailLogQuery(int ClientId, int EmailLogId);
