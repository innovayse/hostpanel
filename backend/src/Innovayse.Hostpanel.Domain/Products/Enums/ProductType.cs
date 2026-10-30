namespace Innovayse.Hostpanel.Domain.Products.Enums;

/// <summary>Classifies a product by the type of service it represents.</summary>
public enum ProductType
{
    /// <summary>Shared web hosting plan.</summary>
    SharedHosting = 0,

    /// <summary>Virtual private server.</summary>
    Vps = 1,

    /// <summary>Dedicated server.</summary>
    Dedicated = 2,

    /// <summary>Domain name registration.</summary>
    Domain = 3,

    /// <summary>SSL certificate.</summary>
    Ssl = 4,

    /// <summary>Other or custom service.</summary>
    Other = 5,

    /// <summary>Mailboxes and mail hosting.</summary>
    Email = 6,

    /// <summary>An extra attached to another service (an IP address, a licence).</summary>
    Addon = 7,

    /// <summary>Backup storage and restore.</summary>
    Backup = 8,

    /// <summary>
    /// A digital good (a licence, a credit pack, a plan) delivered by a fulfilment plugin keyed on the
    /// product's SKU and metadata; nothing is provisioned on a hosting account.
    /// </summary>
    Digital = 9,
}
