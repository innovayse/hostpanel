namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// DTO carrying the WHOIS registrant contact the registrar currently holds for a domain. Shared by
/// the admin and the client read, which return the same shape.
/// </summary>
/// <param name="FirstName">Registrant first name.</param>
/// <param name="LastName">Registrant last name.</param>
/// <param name="Organization">Organization or company name; null when the registrant has none.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact phone number.</param>
/// <param name="Address1">Primary street address.</param>
/// <param name="Address2">Secondary address line; null when there is none.</param>
/// <param name="City">City name.</param>
/// <param name="State">State or province.</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
/// <param name="ExtendedAttributes">The registry's extra values of the domain by field key, as last sent to the registrar; empty when it asked for none.</param>
public sealed record DomainContactDto(
    string FirstName,
    string LastName,
    string? Organization,
    string Email,
    string Phone,
    string Address1,
    string? Address2,
    string City,
    string State,
    string PostalCode,
    string Country,
    IReadOnlyDictionary<string, string> ExtendedAttributes);
