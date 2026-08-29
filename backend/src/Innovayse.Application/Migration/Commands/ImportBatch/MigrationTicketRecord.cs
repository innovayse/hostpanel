namespace Innovayse.Application.Migration.Commands.ImportBatch;

/// <summary>A single support ticket record.</summary>
/// <param name="ClientEmail">Email of the client who opened the ticket.</param>
/// <param name="Subject">Ticket subject.</param>
/// <param name="Message">Opening message body.</param>
/// <param name="Status">Ticket status string.</param>
/// <param name="Priority">Ticket priority string.</param>
/// <param name="CreatedAt">When the ticket was created.</param>
public sealed record MigrationTicketRecord(
    string ClientEmail, string Subject, string Message,
    string Status, string Priority, DateTimeOffset CreatedAt);
