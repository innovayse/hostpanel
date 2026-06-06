namespace Innovayse.Application.Support.Commands.LeaveFeedback;

/// <summary>Records client feedback (rating + comment) for a ticket.</summary>
public sealed record LeaveFeedbackCommand(int TicketId, int Rating, string? Comment, string LeftBy);
