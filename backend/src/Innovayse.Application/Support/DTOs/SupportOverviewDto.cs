namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO for the support overview dashboard stats.</summary>
/// <param name="NewTickets">Number of new tickets in the period.</param>
/// <param name="ClientReplies">Number of client replies in the period.</param>
/// <param name="StaffReplies">Number of staff replies in the period.</param>
/// <param name="TicketsWithoutReply">Number of tickets with no staff reply in the period.</param>
/// <param name="AverageFirstResponse">Average time to first staff response, or null when no data.</param>
/// <param name="TicketsByHour">Array of 24 integers representing ticket counts per hour (0-23).</param>
public record SupportOverviewDto(
    int NewTickets,
    int ClientReplies,
    int StaffReplies,
    int TicketsWithoutReply,
    string? AverageFirstResponse,
    int[] TicketsByHour);
