namespace Innovayse.Application.Domains.Commands.RenewDomain;

/// <summary>Command to renew an existing domain registration for additional years.</summary>
/// <param name="DomainId">Primary key of the domain to renew.</param>
/// <param name="Years">Number of years to extend the registration (1–10).</param>
public record RenewDomainCommand(int DomainId, int Years);
