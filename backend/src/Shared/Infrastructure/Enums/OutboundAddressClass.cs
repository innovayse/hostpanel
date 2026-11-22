namespace Innovayse.Hostpanel.Shared.Infrastructure.Enums;

/// <summary>How <see cref="OutboundAddressPolicy"/> classifies an address this server is asked to dial.</summary>
public enum OutboundAddressClass
{
    /// <summary>A public address: always allowed.</summary>
    Public,

    /// <summary>
    /// A private network address (10/8, 172.16/12, 192.168/16, 100.64/10, fc00::/7): allowed only when
    /// the deployment allows private networks, which it does by default.
    /// </summary>
    Private,

    /// <summary>
    /// Loopback, link-local, a cloud metadata endpoint, unspecified, multicast or reserved: never
    /// dialled, whatever the options say.
    /// </summary>
    Refused,
}
