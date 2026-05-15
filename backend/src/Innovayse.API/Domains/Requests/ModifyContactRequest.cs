namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for modifying WHOIS registrant contact details.</summary>
public sealed class ModifyContactRequest
{
    /// <summary>Registrant first name.</summary>
    public required string FirstName { get; init; }

    /// <summary>Registrant last name.</summary>
    public required string LastName { get; init; }

    /// <summary>Organization name.</summary>
    public string? Organization { get; init; }

    /// <summary>Contact email.</summary>
    public required string Email { get; init; }

    /// <summary>Contact phone.</summary>
    public required string Phone { get; init; }

    /// <summary>Street address.</summary>
    public required string Address1 { get; init; }

    /// <summary>Secondary address line.</summary>
    public string? Address2 { get; init; }

    /// <summary>City.</summary>
    public required string City { get; init; }

    /// <summary>State or province.</summary>
    public required string State { get; init; }

    /// <summary>Postal code.</summary>
    public required string PostalCode { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public required string Country { get; init; }
}
