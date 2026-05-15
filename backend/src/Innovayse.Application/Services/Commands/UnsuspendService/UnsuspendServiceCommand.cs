namespace Innovayse.Application.Services.Commands.UnsuspendService;

/// <summary>Command to unsuspend a previously suspended client service.</summary>
/// <param name="ServiceId">The service primary key.</param>
public record UnsuspendServiceCommand(int ServiceId);
