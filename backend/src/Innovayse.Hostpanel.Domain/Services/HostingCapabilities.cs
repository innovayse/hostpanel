namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>What a server's hosting provider reports it can set up, so no one asks it for what it cannot do.</summary>
/// <param name="Websites">Whether it can create and remove websites.</param>
/// <param name="Databases">Whether it can create and remove databases.</param>
/// <param name="PhpVersions">The PHP versions it offers, as <c>N.N</c>, sorted; empty when it offers none.</param>
public sealed record HostingCapabilities(bool Websites, bool Databases, IReadOnlyList<string> PhpVersions);
