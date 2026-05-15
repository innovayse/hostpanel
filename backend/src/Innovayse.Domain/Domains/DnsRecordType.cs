namespace Innovayse.Domain.Domains;

/// <summary>Supported DNS record types.</summary>
public enum DnsRecordType
{
    /// <summary>IPv4 address record.</summary>
    A,

    /// <summary>IPv6 address record.</summary>
    AAAA,

    /// <summary>Canonical name (alias) record.</summary>
    CNAME,

    /// <summary>Mail exchange record.</summary>
    MX,

    /// <summary>Text record — used for SPF, DKIM, verification, etc.</summary>
    TXT,

    /// <summary>Name server record.</summary>
    NS,

    /// <summary>Service locator record.</summary>
    SRV,
}
