namespace Innovayse.API.Support.Requests;

/// <summary>Request body for leaving feedback on a ticket.</summary>
public sealed record LeaveFeedbackRequest(int Rating, string? Comment, string LeftBy);
