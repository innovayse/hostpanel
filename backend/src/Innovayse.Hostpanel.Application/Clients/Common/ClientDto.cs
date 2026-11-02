using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>Full details DTO for a client, including contacts and billing address.</summary>
/// <param name="Id">Client primary key.</param>
/// <param name="UserId">Linked Identity user ID.</param>
/// <param name="Email">Email address from the linked Identity user.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Status">Current account status.</param>
/// <param name="Address">Billing address.</param>
/// <param name="Preferences">Language, currency, payment preferences and the internal admin notes.</param>
/// <param name="Notifications">Which kinds of email the client receives.</param>
/// <param name="Settings">The account switches staff set on the client.</param>
/// <param name="TwoFactorEnabled">True if the user has TOTP 2FA enabled.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="Contacts">Additional contacts linked to this client.</param>
/// <param name="ExternalAccount">The account in another system the client is linked to, null when there is no link.</param>
/// <param name="CreditBalance">Credit held on the account, in the billing currency; while it is not zero the currency cannot change.</param>
public sealed record ClientDto(
    int Id,
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    ClientStatus Status,
    PostalAddress Address,
    ClientPreferencesDto Preferences,
    ContactNotifications Notifications,
    ClientSettingsDto Settings,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ContactDto> Contacts,
    ExternalAccountDto? ExternalAccount,
    decimal CreditBalance);
