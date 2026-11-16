using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateMyProfile;

/// <summary>
/// Partial update of the authenticated client's own profile, billing address and email
/// preferences. Every member is optional: a member that is absent (null) leaves the stored value
/// as it is.
/// </summary>
/// <remarks>
/// Carries only what a client may edit about themselves. No client id (the handler resolves the
/// caller from the credential), no sign-in address (that changes only through a password-checked,
/// mailed confirmation, <c>StartMyEmailChangeCommand</c>), and none of the staff-owned fields: currency, billing contact,
/// admin notes, tax exemption, the other billing settings and status are not on this command at all, so no body can reach
/// them through this route. The one billing field a client enters is their own VAT number.
/// </remarks>
/// <param name="FirstName">New first name, or null to keep the current one.</param>
/// <param name="LastName">New last name, or null to keep the current one.</param>
/// <param name="CompanyName">New company name (empty to clear), or null to keep the current one.</param>
/// <param name="Phone">New phone number (empty to clear), or null to keep the current one.</param>
/// <param name="Address">New billing address, or null to keep the stored one; a line left null inside it keeps that stored line, an empty one clears it. The country is an ISO 3166-1 alpha-2 code.</param>
/// <param name="Language">Preferred UI language code (empty for no preference), or null to keep the current one.</param>
/// <param name="PaymentMethod">New default payment method, or null to keep the current one.</param>
/// <param name="Notifications">New email notification flags, replaced as a group, or null to keep the stored ones.</param>
/// <param name="VatNumber">New VAT number, with or without its country prefix (empty to clear), or null to keep the current one. It is checked against the VAT number service once saved.</param>
public sealed record UpdateMyProfileCommand(
    string? FirstName = null,
    string? LastName = null,
    string? CompanyName = null,
    string? Phone = null,
    PostalAddress? Address = null,
    string? Language = null,
    string? PaymentMethod = null,
    ContactNotifications? Notifications = null,
    string? VatNumber = null);
