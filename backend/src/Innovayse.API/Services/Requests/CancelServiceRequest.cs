namespace Innovayse.API.Services.Requests;

/// <summary>Request body for submitting a service cancellation.</summary>
/// <param name="Type">Cancellation type: "Immediate" or "EndOfBillingPeriod".</param>
/// <param name="Reason">Optional client-supplied reason for cancellation.</param>
public record CancelServiceRequest(string Type, string? Reason);
