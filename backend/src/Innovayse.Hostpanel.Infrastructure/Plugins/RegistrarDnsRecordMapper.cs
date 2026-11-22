using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one mapping between the host's <see cref="DnsRecord"/> and the SDK's <see cref="RegistrarDnsRecord"/>. The host
/// has no CAA record type, so a CAA record read from a registrar is left out and one can never be written.
/// </summary>
public static class RegistrarDnsRecordMapper
{
    /// <summary>Maps a host record to the one a plugin receives; the host's id is its own, so no record id is passed on.</summary>
    /// <param name="record">The host's record.</param>
    /// <returns>The same record as a plugin record.</returns>
    public static RegistrarDnsRecord ToPlugin(DnsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new RegistrarDnsRecord(null, ToPluginType(record.Type), record.Host, record.Value, record.Ttl, record.Priority);
    }

    /// <summary>Maps a record a plugin read to a host record that belongs to no domain.</summary>
    /// <param name="record">The plugin's record.</param>
    /// <returns>The record, or <see langword="null"/> when its type is one the host cannot hold.</returns>
    public static DnsRecord? FromPlugin(RegistrarDnsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        DnsRecordType? type = record.Type switch
        {
            RegistrarDnsRecordType.A => DnsRecordType.A,
            RegistrarDnsRecordType.Aaaa => DnsRecordType.AAAA,
            RegistrarDnsRecordType.Cname => DnsRecordType.CNAME,
            RegistrarDnsRecordType.Mx => DnsRecordType.MX,
            RegistrarDnsRecordType.Txt => DnsRecordType.TXT,
            RegistrarDnsRecordType.Ns => DnsRecordType.NS,
            RegistrarDnsRecordType.Srv => DnsRecordType.SRV,
            _ => null,
        };

        return type is { } mapped
            ? DnsRecord.CreateDetached(mapped, record.Host, record.Value, record.Ttl, record.Priority)
            : null;
    }

    /// <summary>Maps a host record type to the plugin's.</summary>
    /// <param name="type">The host's type.</param>
    /// <returns>The plugin's type of the same name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type"/> is not a defined host type.</exception>
    private static RegistrarDnsRecordType ToPluginType(DnsRecordType type)
    {
        return type switch
        {
            DnsRecordType.A => RegistrarDnsRecordType.A,
            DnsRecordType.AAAA => RegistrarDnsRecordType.Aaaa,
            DnsRecordType.CNAME => RegistrarDnsRecordType.Cname,
            DnsRecordType.MX => RegistrarDnsRecordType.Mx,
            DnsRecordType.TXT => RegistrarDnsRecordType.Txt,
            DnsRecordType.NS => RegistrarDnsRecordType.Ns,
            DnsRecordType.SRV => RegistrarDnsRecordType.Srv,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown DNS record type."),
        };
    }
}
