namespace Innovayse.Application.Reports.Common;

/// <summary>One tag with its usage count.</summary>
/// <param name="Tag">The tag text.</param>
/// <param name="Count">How many tickets carry the tag in the reported period.</param>
public record TicketTagRowDto(string Tag, int Count);
