namespace Innovayse.API.Support.Requests;

/// <summary>Request body for bulk ticket actions.</summary>
/// <param name="TicketIds">Array of ticket IDs to act upon.</param>
/// <param name="Action">Action to perform: "close", "delete", "flag", "unflag".</param>
public record BulkActionRequest(int[] TicketIds, string Action);
