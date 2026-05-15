namespace Innovayse.Domain.Clients;

/// <summary>
/// Granular permissions that can be assigned to non-owner users linked to a client account.
/// Stored as a bit-flags integer — multiple permissions are combined via bitwise OR.
/// </summary>
[Flags]
public enum ClientPermission : int
{
    /// <summary>No permissions.</summary>
    None = 0,

    /// <summary>Modify the master account profile.</summary>
    ModifyMasterProfile = 1 << 0,

    /// <summary>View and manage contacts.</summary>
    ViewManageContacts = 1 << 1,

    /// <summary>View products and services.</summary>
    ViewProductsServices = 1 << 2,

    /// <summary>View and modify product passwords.</summary>
    ViewModifyPasswords = 1 << 3,

    /// <summary>Allow single sign-on to hosting panels.</summary>
    AllowSingleSignOn = 1 << 4,

    /// <summary>View domains.</summary>
    ViewDomains = 1 << 5,

    /// <summary>Manage domain settings (DNS, nameservers, etc.).</summary>
    ManageDomainSettings = 1 << 6,

    /// <summary>View and pay invoices.</summary>
    ViewPayInvoices = 1 << 7,

    /// <summary>View and accept quotes.</summary>
    ViewAcceptQuotes = 1 << 8,

    /// <summary>View and open support tickets.</summary>
    ViewOpenSupportTickets = 1 << 9,

    /// <summary>View and manage affiliate account.</summary>
    ViewManageAffiliate = 1 << 10,

    /// <summary>View emails sent to the account.</summary>
    ViewEmails = 1 << 11,

    /// <summary>Place new orders, upgrades, and cancellations.</summary>
    PlaceNewOrders = 1 << 12,

    /// <summary>All permissions combined. Equivalent to the account owner.</summary>
    All = 0x1FFF
}
