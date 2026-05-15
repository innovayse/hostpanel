namespace Innovayse.Application.Services.Commands.CancelService;

/// <summary>Command to submit a cancellation request for a client service.</summary>
/// <param name="ServiceId">The client service primary key.</param>
/// <param name="Type">Cancellation type: "Immediate" or "EndOfBillingPeriod".</param>
/// <param name="Reason">Optional client-supplied reason.</param>
public record CancelServiceCommand(int ServiceId, string Type, string? Reason);
