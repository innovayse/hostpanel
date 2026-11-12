namespace Innovayse.Hostpanel.Domain.Fraud.Enums;

/// <summary>What a block list entry's value names.</summary>
public enum BlockListType
{
    /// <summary>A single IP address.</summary>
    Ip = 1,

    /// <summary>A range of IP addresses in CIDR notation.</summary>
    Cidr = 2,

    /// <summary>An e-mail domain; its subdomains are blocked with it.</summary>
    EmailDomain = 3,

    /// <summary>A country by its ISO 3166-1 alpha-2 code.</summary>
    Country = 4,
}
