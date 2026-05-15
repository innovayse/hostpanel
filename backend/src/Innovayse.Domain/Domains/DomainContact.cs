namespace Innovayse.Domain.Domains;

/// <summary>
/// Value object representing WHOIS registrant contact details.
/// Not persisted — used as a parameter for registrar API calls.
/// </summary>
/// <param name="FirstName">Registrant first name.</param>
/// <param name="LastName">Registrant last name.</param>
/// <param name="Organization">Organization or company name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact phone number.</param>
/// <param name="Address1">Primary street address.</param>
/// <param name="Address2">Secondary address line.</param>
/// <param name="City">City name.</param>
/// <param name="State">State or province.</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
public record DomainContact(
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
    string Country);
