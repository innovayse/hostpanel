namespace Innovayse.API.Support.Requests;

/// <summary>Request body for assigning a ticket to a staff member.</summary>
public sealed class AssignTicketRequest
{
    /// <summary>Gets or sets the staff member ID to assign the ticket to.</summary>
    public required int StaffId { get; init; }
}
