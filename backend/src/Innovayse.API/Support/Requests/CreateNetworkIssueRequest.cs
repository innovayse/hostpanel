namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a network issue.</summary>
public sealed class CreateNetworkIssueRequest
{
    /// <summary>Gets the issue title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the type of network issue (Server, Other).</summary>
    public required string Type { get; init; }

    /// <summary>Gets the affected server name, if applicable.</summary>
    public string? Server { get; init; }

    /// <summary>Gets the priority level (Low, Medium, High, Critical).</summary>
    public required string Priority { get; init; }

    /// <summary>Gets the UTC timestamp when the issue started.</summary>
    public required DateTimeOffset StartDate { get; init; }

    /// <summary>Gets the HTML description of the issue.</summary>
    public required string Description { get; init; }
}
