using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// Maps a <see cref="Client"/> aggregate to the staff <see cref="ClientDto"/> and to the client's own
/// <see cref="MyProfileDto"/>, and a <see cref="Contact"/> to its <see cref="ContactDto"/>. Everything that
/// shows a contact uses the one contact mapping, so a contact field is added once. The grouped parts
/// (address, notification flags, settings) have one mapping each as well.
/// </summary>
public static class ClientMappingExtensions
{
    /// <summary>Maps a <see cref="Client"/> aggregate, with its contacts loaded, to <see cref="ClientDto"/>.</summary>
    /// <param name="client">The client aggregate to map; its contacts must be loaded.</param>
    /// <param name="email">The email from the Identity user.</param>
    /// <param name="twoFactorEnabled">Whether TOTP 2FA is enabled for the user.</param>
    /// <param name="language">The account's chosen UI language, null where the provider holds none.</param>
    /// <returns>The mapped DTO.</returns>
    public static ClientDto ToDto(this Client client, string email, bool twoFactorEnabled, string? language) =>
        new(
            client.Id,
            client.UserId,
            email,
            client.FirstName,
            client.LastName,
            client.CompanyName,
            client.Phone,
            client.Status,
            client.ToAddress(),
            // The language comes from the account, not the client row: it belongs to the person who
            // signs in, and this product's own table is the only store it ever lived in.
            new ClientPreferencesDto(language, client.Currency, client.PaymentMethod, client.BillingContact, client.AdminNotes),
            client.ToNotifications(),
            client.ToSettings(),
            twoFactorEnabled,
            client.CreatedAt,
            client.Contacts.Select(ToContactDto).ToList(),
            // The two halves are set and cleared together, so one being present means both are.
            client.ExternalAccountProvider is null || client.ExternalAccountId is null
                ? null
                : new ExternalAccountDto(client.ExternalAccountProvider, client.ExternalAccountId),
            client.CreditBalance);

    /// <summary>
    /// Maps a <see cref="Client"/> aggregate, with its contacts loaded, to the client's own
    /// <see cref="MyProfileDto"/>, which carries none of the staff-owned fields.
    /// </summary>
    /// <param name="client">The client aggregate to map; its contacts must be loaded.</param>
    /// <param name="email">The email from the Identity user.</param>
    /// <param name="twoFactorEnabled">Whether TOTP 2FA is enabled for the user.</param>
    /// <param name="language">The account's chosen UI language, null where the provider holds none.</param>
    /// <returns>The mapped DTO.</returns>
    public static MyProfileDto ToMyProfileDto(this Client client, string email, bool twoFactorEnabled, string? language) =>
        new(
            client.Id,
            email,
            client.FirstName,
            client.LastName,
            client.CompanyName,
            client.Phone,
            client.ToAddress(),
            language,
            client.Currency,
            client.PaymentMethod,
            new MyProfileNotificationsDto(
                client.NotifyGeneral,
                client.NotifyInvoice,
                client.NotifySupport,
                client.NotifyProduct,
                client.NotifyDomain,
                client.NotifyAffiliate,
                client.MarketingOptIn,
                client.StatusUpdate),
            twoFactorEnabled,
            client.Contacts.Select(ToContactDto).ToList());

    /// <summary>Maps one contact of a client to <see cref="ContactDto"/>.</summary>
    /// <param name="contact">The contact to map.</param>
    /// <returns>The mapped DTO.</returns>
    public static ContactDto ToContactDto(this Contact contact) =>
        new(
            contact.Id, contact.FirstName, contact.LastName, contact.CompanyName,
            contact.Email, contact.Phone, contact.Type,
            contact.ToAddress(),
            contact.ToNotifications());

    /// <summary>Reads the billing address off a client.</summary>
    /// <param name="client">The client record read from.</param>
    /// <returns>The billing address.</returns>
    public static PostalAddress ToAddress(this Client client) =>
        new(client.Street, client.Address2, client.City, client.State, client.PostCode, client.Country);

    /// <summary>Reads the postal address off a contact.</summary>
    /// <param name="contact">The contact record read from.</param>
    /// <returns>The postal address.</returns>
    public static PostalAddress ToAddress(this Contact contact) =>
        new(contact.Street, contact.Address2, contact.City, contact.State, contact.PostCode, contact.Country);

    /// <summary>Reads the email notification flags off a client.</summary>
    /// <param name="client">The client record read from.</param>
    /// <returns>Which kinds of email the client receives.</returns>
    public static ContactNotifications ToNotifications(this Client client) =>
        new(
            client.NotifyGeneral, client.NotifyInvoice, client.NotifySupport,
            client.NotifyProduct, client.NotifyDomain, client.NotifyAffiliate);

    /// <summary>Reads the email notification flags off a contact.</summary>
    /// <param name="contact">The contact record read from.</param>
    /// <returns>Which kinds of email the contact receives.</returns>
    public static ContactNotifications ToNotifications(this Contact contact) =>
        new(
            contact.NotifyGeneral, contact.NotifyInvoice, contact.NotifySupport,
            contact.NotifyProduct, contact.NotifyDomain, contact.NotifyAffiliate);

    /// <summary>Reads the account switches off a client.</summary>
    /// <param name="client">The client record read from.</param>
    /// <returns>The account switches staff set on the client.</returns>
    public static ClientSettingsDto ToSettings(this Client client) =>
        new(
            client.OverdueNotices, client.TaxExempt, client.SeparateInvoices, client.DisableCcProcessing,
            client.MarketingOptIn, client.StatusUpdate, client.AllowSso);
}
