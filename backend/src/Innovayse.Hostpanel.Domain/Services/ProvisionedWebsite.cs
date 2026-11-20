namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>What a hosting provider reports after it set a website up.</summary>
/// <param name="DocumentRoot">The directory the website serves files from, as the provider created it.</param>
/// <param name="PhpVersion">The PHP version the website runs, or <see langword="null"/> when it runs none.</param>
public sealed record ProvisionedWebsite(string DocumentRoot, string? PhpVersion);
