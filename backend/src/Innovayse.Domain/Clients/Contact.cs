namespace Innovayse.Domain.Clients;

using Innovayse.Domain.Common;

/// <summary>
/// An additional contact linked to a <see cref="Client"/> account.
/// Stored in the <c>contacts</c> table.
/// </summary>
public sealed class Contact : Entity
{
    /// <summary>Gets the ID of the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the contact's first name.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Gets the contact's last name.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Gets the optional company name.</summary>
    public string? CompanyName { get; private set; }

    /// <summary>Gets the contact's email address.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Gets the contact's phone number, or <see langword="null"/> if not provided.</summary>
    public string? Phone { get; private set; }

    /// <summary>Gets the type of contact (billing, technical, general).</summary>
    public ContactType Type { get; private set; }

    /// <summary>Gets the street address.</summary>
    public string? Street { get; private set; }

    /// <summary>Gets the second address line.</summary>
    public string? Address2 { get; private set; }

    /// <summary>Gets the city.</summary>
    public string? City { get; private set; }

    /// <summary>Gets the state or region.</summary>
    public string? State { get; private set; }

    /// <summary>Gets the postal code.</summary>
    public string? PostCode { get; private set; }

    /// <summary>Gets the ISO 3166-1 alpha-2 country code.</summary>
    public string? Country { get; private set; }

    /// <summary>Gets whether general email notifications are enabled.</summary>
    public bool NotifyGeneral { get; private set; } = true;

    /// <summary>Gets whether invoice email notifications are enabled.</summary>
    public bool NotifyInvoice { get; private set; } = true;

    /// <summary>Gets whether support email notifications are enabled.</summary>
    public bool NotifySupport { get; private set; } = true;

    /// <summary>Gets whether product email notifications are enabled.</summary>
    public bool NotifyProduct { get; private set; } = true;

    /// <summary>Gets whether domain email notifications are enabled.</summary>
    public bool NotifyDomain { get; private set; } = true;

    /// <summary>Gets whether affiliate email notifications are enabled.</summary>
    public bool NotifyAffiliate { get; private set; } = true;

    /// <summary>Gets the UTC timestamp when this contact was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Contact() : base(0) { }

    /// <summary>
    /// Creates a new contact for a client.
    /// </summary>
    /// <param name="clientId">The owning client's ID.</param>
    /// <param name="firstName">The contact's first name.</param>
    /// <param name="lastName">The contact's last name.</param>
    /// <param name="companyName">Optional company name.</param>
    /// <param name="email">The contact's email address.</param>
    /// <param name="phone">Optional phone number.</param>
    /// <param name="type">The contact type classification.</param>
    /// <param name="street">Optional street address.</param>
    /// <param name="address2">Optional second address line.</param>
    /// <param name="city">Optional city.</param>
    /// <param name="state">Optional state or region.</param>
    /// <param name="postCode">Optional postal code.</param>
    /// <param name="country">Optional ISO 3166-1 alpha-2 country code.</param>
    /// <param name="notifyGeneral">General email notifications.</param>
    /// <param name="notifyInvoice">Invoice email notifications.</param>
    /// <param name="notifySupport">Support email notifications.</param>
    /// <param name="notifyProduct">Product email notifications.</param>
    /// <param name="notifyDomain">Domain email notifications.</param>
    /// <param name="notifyAffiliate">Affiliate email notifications.</param>
    internal Contact(
        int clientId, string firstName, string lastName, string? companyName,
        string email, string? phone, ContactType type,
        string? street, string? address2, string? city, string? state, string? postCode, string? country,
        bool notifyGeneral, bool notifyInvoice, bool notifySupport,
        bool notifyProduct, bool notifyDomain, bool notifyAffiliate) : base(0)
    {
        ClientId = clientId;
        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
        Email = email;
        Phone = phone;
        Type = type;
        Street = street;
        Address2 = address2;
        City = city;
        State = state;
        PostCode = postCode;
        Country = country;
        NotifyGeneral = notifyGeneral;
        NotifyInvoice = notifyInvoice;
        NotifySupport = notifySupport;
        NotifyProduct = notifyProduct;
        NotifyDomain = notifyDomain;
        NotifyAffiliate = notifyAffiliate;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates all editable fields on this contact.
    /// </summary>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="companyName">New company name (null to clear).</param>
    /// <param name="email">New email address.</param>
    /// <param name="phone">New phone number (null to clear).</param>
    /// <param name="type">New contact type.</param>
    /// <param name="street">New street address (null to clear).</param>
    /// <param name="address2">New second address line (null to clear).</param>
    /// <param name="city">New city (null to clear).</param>
    /// <param name="state">New state or region (null to clear).</param>
    /// <param name="postCode">New postal code (null to clear).</param>
    /// <param name="country">New ISO 3166-1 alpha-2 country code (null to clear).</param>
    /// <param name="notifyGeneral">General email notifications.</param>
    /// <param name="notifyInvoice">Invoice email notifications.</param>
    /// <param name="notifySupport">Support email notifications.</param>
    /// <param name="notifyProduct">Product email notifications.</param>
    /// <param name="notifyDomain">Domain email notifications.</param>
    /// <param name="notifyAffiliate">Affiliate email notifications.</param>
    internal void Update(
        string firstName, string lastName, string? companyName,
        string email, string? phone, ContactType type,
        string? street, string? address2, string? city, string? state, string? postCode, string? country,
        bool notifyGeneral, bool notifyInvoice, bool notifySupport,
        bool notifyProduct, bool notifyDomain, bool notifyAffiliate)
    {
        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
        Email = email;
        Phone = phone;
        Type = type;
        Street = street;
        Address2 = address2;
        City = city;
        State = state;
        PostCode = postCode;
        Country = country;
        NotifyGeneral = notifyGeneral;
        NotifyInvoice = notifyInvoice;
        NotifySupport = notifySupport;
        NotifyProduct = notifyProduct;
        NotifyDomain = notifyDomain;
        NotifyAffiliate = notifyAffiliate;
    }
}
