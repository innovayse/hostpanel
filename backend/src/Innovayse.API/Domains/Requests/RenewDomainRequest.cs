namespace Innovayse.API.Domains.Requests;

/// <summary>Request payload for renewing a domain registration.</summary>
public sealed class RenewDomainRequest
{
    /// <summary>Gets the number of years to extend the domain registration (1–10).</summary>
    public required int Years { get; init; }
}
