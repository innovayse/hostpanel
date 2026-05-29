namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating a predefined reply.</summary>
public sealed class UpdatePredefinedReplyRequest
{
    /// <summary>Gets the new reply name / label.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the new reply content text.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the new category FK.</summary>
    public required int CategoryId { get; init; }
}
