namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a new support ticket.</summary>
public sealed class CreateTicketRequest
{
    /// <summary>Gets or sets the ticket subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets or sets the initial message body.</summary>
    public required string Message { get; init; }

    /// <summary>Gets or sets the target department ID.</summary>
    public required int DepartmentId { get; init; }

    /// <summary>Gets or sets the priority level (Low, Medium, High, Critical).</summary>
    public required string Priority { get; init; }
}
