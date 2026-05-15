namespace Innovayse.API.Domains.Requests;

/// <summary>Request payload for registering a new domain.</summary>
public sealed class RegisterDomainRequest
{
    /// <summary>Gets the domain name to register (e.g., "example.com").</summary>
    public required string Name { get; init; }

    /// <summary>Gets the registration period in years (1–10).</summary>
    public required int Years { get; init; }

    /// <summary>Gets the client ID to assign the domain to.</summary>
    public required int ClientId { get; init; }

    /// <summary>Gets the registrant contact information.</summary>
    public required WhoisInfoRequest Registrant { get; init; }

    /// <summary>Gets a value indicating whether WHOIS privacy should be enabled.</summary>
    public bool EnableWhoisPrivacy { get; init; }

    /// <summary>Gets a value indicating whether auto-renew should be enabled.</summary>
    public bool AutoRenew { get; init; }
}
