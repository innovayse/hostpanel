using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// The client's own profile as the client area reads it: identity, billing address and the
/// preferences the client may change. Staff-owned fields of <see cref="ClientDto"/> (internal
/// notes, account status, late-fee, tax and processing switches) are not part of it, so they
/// cannot reach the client by any path.
/// </summary>
/// <param name="Id">Client primary key.</param>
/// <param name="Email">Email address from the linked account.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Address">Billing address.</param>
/// <param name="Language">The account holder's chosen UI language, or null where this deployment holds none.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="PaymentMethod">Preferred payment method.</param>
/// <param name="Notifications">Which emails the client receives.</param>
/// <param name="VatNumber">The client's VAT number (their tax identification number), or null when none is recorded.</param>
/// <param name="Vat">The last completed check of that number.</param>
/// <param name="TwoFactorEnabled">True if the user has TOTP 2FA enabled.</param>
/// <param name="Contacts">Additional contacts linked to this client.</param>
public sealed record MyProfileDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    PostalAddress Address,
    string? Language,
    string? Currency,
    string? PaymentMethod,
    MyProfileNotificationsDto Notifications,
    string? VatNumber,
    ClientVatDto Vat,
    bool TwoFactorEnabled,
    IReadOnlyList<ContactDto> Contacts);
