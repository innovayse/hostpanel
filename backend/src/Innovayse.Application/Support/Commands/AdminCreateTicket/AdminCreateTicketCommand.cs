namespace Innovayse.Application.Support.Commands.AdminCreateTicket;

/// <summary>Command for an admin to create a support ticket on behalf of a client.</summary>
/// <param name="ClientId">FK to the client.</param>
/// <param name="Subject">The ticket subject line.</param>
/// <param name="Message">The initial message body.</param>
/// <param name="DepartmentId">FK to the target department.</param>
/// <param name="Priority">Priority level as a string (Low, Medium, High).</param>
public record AdminCreateTicketCommand(
    int ClientId,
    string Subject,
    string Message,
    int DepartmentId,
    string Priority);
