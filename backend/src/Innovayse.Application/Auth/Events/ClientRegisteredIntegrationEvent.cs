namespace Innovayse.Application.Auth.Events;

/// <summary>
/// Integration event published by <c>RegisterHandler</c> after a new Identity user is created.
/// Wolverine delivers it to <c>CreateClientOnRegisterHandler</c> in the Clients module.
/// </summary>
/// <param name="UserId">The new user's Identity ID.</param>
/// <param name="Email">The new user's email address.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="IpAddress">IP address captured during registration; null if unavailable.</param>
/// <param name="UserAgent">Raw browser/device user-agent captured during registration; null if unavailable.</param>
/// <param name="DeviceType">Parsed device type (Desktop, Mobile, Tablet); null if unavailable.</param>
/// <param name="OperatingSystem">Parsed OS from user-agent; null if unavailable.</param>
/// <param name="Browser">Parsed browser name and version; null if unavailable.</param>
public record ClientRegisteredIntegrationEvent(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? IpAddress = null,
    string? UserAgent = null,
    string? DeviceType = null,
    string? OperatingSystem = null,
    string? Browser = null);
