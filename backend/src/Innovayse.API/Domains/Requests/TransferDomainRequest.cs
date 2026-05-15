namespace Innovayse.API.Domains.Requests;

/// <summary>Request payload for initiating an incoming domain transfer.</summary>
public sealed class TransferDomainRequest
{
    /// <summary>Gets the fully-qualified domain name to transfer (e.g., "example.com").</summary>
    public required string Name { get; init; }

    /// <summary>Gets the EPP/authorization code obtained from the losing registrar.</summary>
    public required string EppCode { get; init; }

    /// <summary>Gets the client ID to assign the transferred domain to.</summary>
    public required int ClientId { get; init; }

    /// <summary>Gets the registrant contact information.</summary>
    public required WhoisInfoRequest Registrant { get; init; }

    /// <summary>Gets a value indicating whether to enable WHOIS privacy after the transfer completes.</summary>
    public bool EnableWhoisPrivacy { get; init; }
}
