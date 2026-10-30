using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// Maps what an identity plugin says a principal may do onto the client permission flags the portal
/// enforces, so a plugin member is held to the same checks as a linked user.
/// </summary>
public static class IdentityPluginPermissions
{
    /// <summary>
    /// What every principal the plugin admits may read: services, domains, support tickets and emails.
    /// Nothing here changes anything or touches money.
    /// </summary>
    private const ClientPermission ReadOnly =
        ClientPermission.ViewProductsServices
        | ClientPermission.ViewDomains
        | ClientPermission.ViewOpenSupportTickets
        | ClientPermission.ViewEmails;

    /// <summary>Maps a plugin's answer to client permission flags.</summary>
    /// <param name="access">What the identity plugin granted the principal.</param>
    /// <returns>
    /// The read-only flags; plus <see cref="ClientPermission.PlaceNewOrders"/> and
    /// <see cref="ClientPermission.ViewPayInvoices"/> when the principal may purchase (ordering and paying
    /// invoices); plus <see cref="ClientPermission.ViewPayInvoices"/> when it may manage payment methods,
    /// because the payment method actions are guarded by that flag. The plugin cannot grant anything else.
    /// </returns>
    public static ClientPermission From(ClientAccess access)
    {
        var permissions = ReadOnly;
        if (access.CanPurchase)
        {
            permissions |= ClientPermission.PlaceNewOrders | ClientPermission.ViewPayInvoices;
        }

        if (access.CanManagePaymentMethods)
        {
            permissions |= ClientPermission.ViewPayInvoices;
        }

        return permissions;
    }
}
