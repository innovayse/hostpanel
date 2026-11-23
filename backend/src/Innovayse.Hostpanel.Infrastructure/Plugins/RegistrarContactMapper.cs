using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The one mapping between the host's <see cref="DomainContact"/> and the SDK's <see cref="RegistrarContact"/>.</summary>
public static class RegistrarContactMapper
{
    /// <summary>Maps a host contact to the one a plugin receives.</summary>
    /// <param name="contact">The host's contact.</param>
    /// <returns>The same details as a plugin contact.</returns>
    public static RegistrarContact ToPlugin(DomainContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new RegistrarContact(
            contact.FirstName,
            contact.LastName,
            contact.Organization,
            contact.Email,
            contact.Phone,
            contact.Address1,
            contact.Address2,
            contact.City,
            contact.State,
            contact.PostalCode,
            contact.Country);
    }

    /// <summary>Maps the contact a plugin read to the host's contact.</summary>
    /// <param name="contact">The plugin's contact.</param>
    /// <returns>The same details as a host contact.</returns>
    public static DomainContact FromPlugin(RegistrarContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new DomainContact(
            contact.FirstName,
            contact.LastName,
            contact.Organization,
            contact.Email,
            contact.Phone,
            contact.Address1,
            contact.Address2,
            contact.City,
            contact.State,
            contact.PostalCode,
            contact.Country);
    }
}
