using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Clients;

/// <summary>
/// An additional contact linked to a <see cref="Client"/> account.
/// Stored in the <c>Contacts</c> table.
/// </summary>
public sealed class Contact : BaseEntity
{
    /// <summary>Longest first or last name the stored column holds.</summary>
    private const int NameMaxLength = 100;

    /// <summary>Longest email address the stored column holds.</summary>
    private const int EmailMaxLength = 256;

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

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Contact() { }

    /// <summary>
    /// Creates a new contact for a client.
    /// </summary>
    /// <param name="clientId">The owning client's ID.</param>
    /// <param name="details">What was entered about the contact.</param>
    internal Contact(int clientId, ContactDetails details)
    {
        ClientId = clientId;
        Apply(details);
    }

    /// <summary>
    /// Updates all editable fields on this contact.
    /// </summary>
    /// <param name="details">The new values for every editable field.</param>
    internal void Update(ContactDetails details)
    {
        Apply(details);
    }

    /// <summary>
    /// Writes every editable field from <paramref name="details"/>; the one place a contact's input
    /// is checked and normalised, so creating and updating cannot drift apart.
    /// </summary>
    /// <param name="details">The values to write.</param>
    /// <exception cref="ArgumentException">Thrown when a name is blank or a value is longer than its column.</exception>
    private void Apply(ContactDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        FirstName = Required(details.FirstName, NameMaxLength, nameof(details.FirstName));
        LastName = Required(details.LastName, NameMaxLength, nameof(details.LastName));
        Email = Required(details.Email, EmailMaxLength, nameof(details.Email));
        CompanyName = details.CompanyName;
        Phone = details.Phone;
        Type = details.Type;
        Street = details.Address.Street;
        Address2 = details.Address.Address2;
        City = details.Address.City;
        State = details.Address.State;
        PostCode = details.Address.PostCode;
        Country = CountryCode.Normalize(details.Address.Country);
        NotifyGeneral = details.Notifications.General;
        NotifyInvoice = details.Notifications.Invoice;
        NotifySupport = details.Notifications.Support;
        NotifyProduct = details.Notifications.Product;
        NotifyDomain = details.Notifications.Domain;
        NotifyAffiliate = details.Notifications.Affiliate;
    }

    /// <summary>Checks that a required text value is present and fits its column.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="maxLength">The longest the stored column allows.</param>
    /// <param name="name">The name of the value, for the exception.</param>
    /// <returns>The value, trimmed.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is blank or longer than <paramref name="maxLength"/>.</exception>
    private static string Required(string value, int maxLength, string name)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new ArgumentException($"A contact's {name} is required and at most {maxLength} characters.", name);
        }

        return trimmed;
    }
}
