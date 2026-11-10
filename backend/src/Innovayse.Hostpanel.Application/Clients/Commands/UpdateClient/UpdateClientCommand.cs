using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;

/// <summary>
/// Command to replace an existing client's profile, billing address, preferences,
/// notification settings, billing settings, and status.
/// </summary>
/// <remarks>
/// A full replacement, not a patch: every group is written whole. A notification or billing flag the
/// body leaves out is stored as <c>false</c>, and an optional text field it leaves out is cleared.
/// Only <see cref="Email"/>, <see cref="Preferences"/> language and currency, and <see cref="Status"/>
/// are left alone when null. Callers send the whole record as it was read.
/// </remarks>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Email">Updated email address (null to keep current).</param>
/// <param name="FirstName">Updated first name.</param>
/// <param name="LastName">Updated last name.</param>
/// <param name="CompanyName">Updated company name (null to clear).</param>
/// <param name="Phone">Updated phone number (null to clear).</param>
/// <param name="Address">
/// Updated billing address. Each line null clears it; the country is a 2-character
/// ISO 3166-1 alpha-2 code.
/// </param>
/// <param name="Preferences">
/// Updated preferences. A null language is left alone (written only where this deployment owns the
/// account, and only when it differs from what the account already holds), a null currency keeps the
/// current one, and a null payment method, billing contact or admin notes clears it.
/// </param>
/// <param name="Notifications">Updated email notification flags.</param>
/// <param name="Settings">Updated billing and account switches.</param>
/// <param name="Status">The status to move the client to; null keeps the current one. The move follows the client's own transitions.</param>
/// <param name="ClientGroupId">The client group to put the client in; null takes the client out of its group, like every other optional field of this full replacement.</param>
public sealed record UpdateClientCommand(
    int ClientId,
    string? Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    PostalAddress Address,
    ClientPreferencesDto Preferences,
    ContactNotifications Notifications,
    ClientSettingsDto Settings,
    ClientStatus? Status,
    int? ClientGroupId = null);
