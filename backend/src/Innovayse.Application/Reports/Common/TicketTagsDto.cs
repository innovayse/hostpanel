namespace Innovayse.Application.Reports.Common;

/// <summary>Ticket Tags report result.</summary>
/// <param name="Rows">One entry per distinct tag used in the period.</param>
public record TicketTagsDto(IReadOnlyList<TicketTagRowDto> Rows);
