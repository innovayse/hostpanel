namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>One domain a registrar account holds, as the registrar's domain list reports it.</summary>
/// <param name="Name">The fully qualified domain name, as the registrar writes it.</param>
/// <param name="RegistrarRef">The registrar's own reference for the domain, which the host keeps to address it later.</param>
/// <param name="ExpiresAt">When the registration ends, or <see langword="null"/> when the registrar does not say.</param>
public sealed record RegistrarHeldDomain(string Name, string RegistrarRef, DateTimeOffset? ExpiresAt);
