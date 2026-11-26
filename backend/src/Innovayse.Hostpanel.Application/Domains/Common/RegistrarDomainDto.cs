namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>A domain a registrar account holds, with whether the panel already has it.</summary>
/// <param name="Name">The fully qualified domain name.</param>
/// <param name="ExpiresAt">When the registration ends, or <see langword="null"/> when the registrar does not say (such a domain cannot be imported).</param>
/// <param name="InPanel">Whether a domain of that name is already in the panel, whoever owns it.</param>
public sealed record RegistrarDomainDto(string Name, DateTimeOffset? ExpiresAt, bool InPanel);
