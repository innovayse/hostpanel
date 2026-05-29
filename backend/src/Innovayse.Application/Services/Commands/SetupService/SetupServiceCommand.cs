namespace Innovayse.Application.Services.Commands.SetupService;

/// <summary>Command to set up a pending service with hosting details and trigger provisioning.</summary>
/// <param name="ServiceId">The service to set up.</param>
/// <param name="Domain">The domain name for the hosting account.</param>
/// <param name="Username">The desired hosting account username.</param>
/// <param name="Password">The desired hosting account password.</param>
public record SetupServiceCommand(int ServiceId, string Domain, string Username, string Password);
