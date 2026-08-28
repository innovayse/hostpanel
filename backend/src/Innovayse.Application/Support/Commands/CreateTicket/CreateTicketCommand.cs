namespace Innovayse.Application.Support.Commands.CreateTicket;

/// <summary>Command for a client to open a new support ticket on their own account.</summary>
/// <remarks>
/// Carries no client id. Which account is resolved inside the handler from the credential --
/// an id here would let a caller file a ticket against somebody else's account. The admin
/// route that legitimately opens a ticket for another client is a separate use case,
/// <c>AdminCreateTicketCommand</c>.
/// </remarks>
/// <param name="Subject">The ticket subject line.</param>
/// <param name="Message">The initial message body.</param>
/// <param name="DepartmentId">FK to the target department.</param>
/// <param name="Priority">Priority level string (Low, Medium, High).</param>
public record CreateTicketCommand(string Subject, string Message, int DepartmentId, string Priority);
