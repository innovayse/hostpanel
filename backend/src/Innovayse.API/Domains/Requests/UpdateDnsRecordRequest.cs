namespace Innovayse.API.Domains.Requests;

/// <summary>Request payload for updating an existing DNS record in a domain's zone.</summary>
public sealed class UpdateDnsRecordRequest
{
    /// <summary>Gets the new record value (e.g. IP address, target hostname, TXT string).</summary>
    public required string Value { get; init; }

    /// <summary>Gets the new time-to-live in seconds.</summary>
    public required int Ttl { get; init; }

    /// <summary>Gets the new priority for MX/SRV records; null to clear priority.</summary>
    public int? Priority { get; init; }
}
