namespace Innovayse.Infrastructure.Domains.Namecheap;

/// <summary>
/// Configuration settings for the Namecheap XML API v2 integration.
/// Bind from <c>appsettings.json</c> section <c>Namecheap</c>.
/// </summary>
public sealed class NamecheapSettings
{
    /// <summary>Gets the Namecheap API user account username.</summary>
    public required string ApiUser { get; init; }

    /// <summary>Gets the Namecheap API key for the account.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Gets the whitelisted client IP address required by the Namecheap API.</summary>
    public required string ClientIp { get; init; }

    /// <summary>Gets the base URL of the Namecheap XML API endpoint.</summary>
    public required string ApiUrl { get; init; }

    /// <summary>Gets a value indicating whether the sandbox (test) environment is active.</summary>
    public bool Sandbox { get; init; }
}
