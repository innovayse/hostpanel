namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating a network issue.</summary>
public sealed class UpdateNetworkIssueRequest
{
    /// <summary>Gets the new issue title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the new type of network issue (Server, Other).</summary>
    public required string Type { get; init; }

    /// <summary>Gets the new affected server name, if applicable.</summary>
    public string? Server { get; init; }

    /// <summary>Gets the new priority level (Low, Medium, High, Critical).</summary>
    public required string Priority { get; init; }

    /// <summary>Gets the new lifecycle status (Reported, Investigating, Scheduled, Resolved).</summary>
    public required string Status { get; init; }

    /// <summary>Gets the new start date.</summary>
    public required DateTimeOffset StartDate { get; init; }

    /// <summary>Gets the new end date, if resolved.</summary>
    public DateTimeOffset? EndDate { get; init; }

    /// <summary>Gets the new HTML description.</summary>
    public required string Description { get; init; }
}
