namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a predefined reply.</summary>
public sealed class CreatePredefinedReplyRequest
{
    /// <summary>Gets the reply name / label.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the reply content text.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the FK to the category.</summary>
    public required int CategoryId { get; init; }
}
