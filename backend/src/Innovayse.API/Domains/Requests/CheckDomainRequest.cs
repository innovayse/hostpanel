namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for the POST domain availability check endpoint.</summary>
public sealed class CheckDomainRequest
{
    /// <summary>The fully-qualified domain name to check (e.g. "example.com").</summary>
    public required string Domain { get; init; }
}
