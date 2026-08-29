namespace Innovayse.Application.Auth.Events;

/// <summary>
/// Integration event published by <c>LocalAuthController.RegisterAsync</c> after a new Identity
/// user is created under local auth mode. Wolverine delivers it to
/// <c>CreateClientOnRegisterHandler</c> in the Clients module.
/// </summary>
/// <remarks>
/// "Integration event" names the intent, not the transport. Wolverine is configured here for
/// handler discovery only — no broker, no publishing rules — so this message never leaves the
/// process and its only subscriber lives in this same assembly.
///
/// <para>
/// The five optional parameters are the surviving half of a removal, and not one of them is
/// populated today: <c>RegisterAsync</c> passes the first four arguments and stops. The capture
/// that filled <c>DeviceType</c>, <c>OperatingSystem</c> and <c>Browser</c> went with local
/// self-registration's original <c>RegisterHandler</c>, and the user-agent parser behind it has
/// now been deleted as dead code. The three are kept anyway, because they are the only route to
/// the <c>Clients.DeviceType</c> / <c>OperatingSystem</c> / <c>Browser</c> columns — still
/// mapped, still migrated — and because cutting three of the five while leaving the equally
/// unfilled <c>IpAddress</c> and <c>UserAgent</c> would be an arbitrary line. Finishing this
/// means filling all five from the request, not trimming the record.
/// </para>
/// </remarks>
/// <param name="UserId">The new user's Identity ID.</param>
/// <param name="Email">The new user's email address.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="IpAddress">IP address captured during registration; never populated at present.</param>
/// <param name="UserAgent">Raw browser/device user-agent captured during registration; never populated at present.</param>
/// <param name="DeviceType">Parsed device type (Desktop, Mobile, Tablet); never populated at present.</param>
/// <param name="OperatingSystem">Parsed OS from the user-agent; never populated at present.</param>
/// <param name="Browser">Parsed browser name and version; never populated at present.</param>
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
