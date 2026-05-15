namespace Innovayse.API.Domains.Requests;

/// <summary>Request payload for replacing a domain's nameserver list.</summary>
public sealed class UpdateNameserversRequest
{
    /// <summary>Gets the ordered list of nameserver hostnames (minimum 2).</summary>
    public required List<string> Nameservers { get; init; }
}
