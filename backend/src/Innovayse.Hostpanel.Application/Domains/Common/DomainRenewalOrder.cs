namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Parameters required to renew a domain registration via a registrar provider.
/// </summary>
/// <param name="DomainName">Fully-qualified domain name to renew.</param>
/// <param name="RegistrarRef">Registrar-assigned reference returned at registration or last renewal.</param>
/// <param name="Years">Number of years to extend the registration.</param>
/// <param name="CurrentExpiresAt">
/// The expiry the host holds before the renewal, so a registrar that already extended the domain can tell a repeated
/// renewal from a new one and the domain is never charged twice.
/// </param>
public record DomainRenewalOrder(string DomainName, string RegistrarRef, int Years, DateTimeOffset? CurrentExpiresAt);
