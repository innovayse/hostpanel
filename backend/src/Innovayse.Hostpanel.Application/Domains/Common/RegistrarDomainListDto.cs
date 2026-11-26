namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>One page of the domains a registrar account holds, for the admin's import screen.</summary>
/// <param name="Domains">The domains of the page.</param>
/// <param name="NextCursor">The cursor to send for the next page, or <see langword="null"/> on the last page.</param>
public sealed record RegistrarDomainListDto(IReadOnlyList<RegistrarDomainDto> Domains, string? NextCursor);
