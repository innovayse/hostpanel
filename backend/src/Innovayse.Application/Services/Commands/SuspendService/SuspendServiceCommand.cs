namespace Innovayse.Application.Services.Commands.SuspendService;

/// <summary>Command to suspend an active client service.</summary>
/// <param name="ServiceId">The service primary key.</param>
public record SuspendServiceCommand(int ServiceId);
