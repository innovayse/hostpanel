namespace Innovayse.Application.Services.Commands.TerminateService;

/// <summary>Command to permanently terminate a client service.</summary>
/// <param name="ServiceId">The service primary key.</param>
public record TerminateServiceCommand(int ServiceId);
