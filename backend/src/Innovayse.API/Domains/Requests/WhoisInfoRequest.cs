namespace Innovayse.API.Domains.Requests;

/// <summary>WHOIS registrant contact details for domain registration or transfer.</summary>
public sealed class WhoisInfoRequest
{
    /// <summary>Gets the registrant's first name.</summary>
    public required string FirstName { get; init; }

    /// <summary>Gets the registrant's last name.</summary>
    public required string LastName { get; init; }

    /// <summary>Gets the registrant's email address.</summary>
    public required string Email { get; init; }

    /// <summary>Gets the registrant's phone number in international format.</summary>
    public required string Phone { get; init; }

    /// <summary>Gets the registrant's street address.</summary>
    public required string Address { get; init; }

    /// <summary>Gets the registrant's city.</summary>
    public required string City { get; init; }

    /// <summary>Gets the registrant's ISO 3166-1 alpha-2 country code.</summary>
    public required string Country { get; init; }

    /// <summary>Gets the registrant's state or province; null if not applicable.</summary>
    public string? State { get; init; }

    /// <summary>Gets the registrant's postal/ZIP code; null if not applicable.</summary>
    public string? PostalCode { get; init; }

    /// <summary>Gets the registrant's organization name; null for individuals.</summary>
    public string? Organization { get; init; }
}
