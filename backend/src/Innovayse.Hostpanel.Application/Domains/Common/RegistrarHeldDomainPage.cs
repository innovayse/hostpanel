namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>One page of the domains a registrar account holds.</summary>
/// <param name="Domains">The domains of the page; empty only on the last page.</param>
/// <param name="NextCursor">The cursor of the next page, as the registrar wrote it, or <see langword="null"/> on the last page.</param>
public sealed record RegistrarHeldDomainPage(IReadOnlyList<RegistrarHeldDomain> Domains, string? NextCursor);
