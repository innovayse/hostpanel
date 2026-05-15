namespace Innovayse.Application.Support.Commands.CreateTicket;

/// <summary>Command to open a new support ticket on behalf of a client.</summary>
/// <param name="ClientId">FK to the client opening the ticket.</param>
/// <param name="Subject">The ticket subject line.</param>
/// <param name="Message">The initial message body.</param>
/// <param name="DepartmentId">FK to the target department.</param>
/// <param name="Priority">Priority level string (Low, Medium, High).</param>
public record CreateTicketCommand(int ClientId, string Subject, string Message, int DepartmentId, string Priority);
