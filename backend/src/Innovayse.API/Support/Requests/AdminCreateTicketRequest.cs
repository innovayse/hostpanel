namespace Innovayse.API.Support.Requests;

/// <summary>Request body for admin ticket creation.</summary>
public sealed class AdminCreateTicketRequest
{
    /// <summary>Gets the client identifier.</summary>
    public required int ClientId { get; init; }

    /// <summary>Gets the ticket subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the initial message body.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the target department identifier.</summary>
    public required int DepartmentId { get; init; }

    /// <summary>Gets the priority level (Low, Medium, High).</summary>
    public required string Priority { get; init; }
}
