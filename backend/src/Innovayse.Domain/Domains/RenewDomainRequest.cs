namespace Innovayse.Domain.Domains;

/// <summary>
/// Parameters required to renew a domain registration via a registrar provider.
/// </summary>
/// <param name="DomainName">Fully-qualified domain name to renew.</param>
/// <param name="RegistrarRef">Registrar-assigned reference returned at registration or last renewal.</param>
/// <param name="Years">Number of years to extend the registration.</param>
public record RenewDomainRequest(string DomainName, string RegistrarRef, int Years);
