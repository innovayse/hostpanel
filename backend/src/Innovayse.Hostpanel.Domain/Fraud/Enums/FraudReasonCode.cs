namespace Innovayse.Hostpanel.Domain.Fraud.Enums;

/// <summary>
/// Why a registration or an order scored risk. The names are stored (in the fraud log and on an order held for review),
/// so a member is never renamed or removed once released; new reasons are added at the end.
/// </summary>
public enum FraudReasonCode
{
    /// <summary>The caller's IP address is on the block list, alone or inside a blocked range.</summary>
    BlockedIp = 1,

    /// <summary>The e-mail address belongs to a blocked domain.</summary>
    BlockedEmailDomain = 2,

    /// <summary>The billing country or the country of the IP address is on the block list.</summary>
    BlockedCountry = 3,

    /// <summary>The e-mail address belongs to a disposable mailbox service.</summary>
    DisposableEmail = 4,

    /// <summary>More accounts than the limit were used from the same IP address.</summary>
    SharedIpAccounts = 5,

    /// <summary>More orders than the limit were placed from the same IP address within the velocity window.</summary>
    OrderVelocity = 6,

    /// <summary>The billing country differs from the country of the IP address.</summary>
    CountryMismatch = 7,

    /// <summary>The external scoring service rated the request as high risk.</summary>
    ProviderHighRisk = 8,

    /// <summary>The external scoring service saw a proxy, VPN, Tor exit or hosting address behind the request.</summary>
    AnonymousIp = 9,
}
