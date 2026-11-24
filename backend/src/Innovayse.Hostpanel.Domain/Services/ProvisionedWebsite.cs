namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>What a hosting provider reports after it set a website up.</summary>
/// <param name="PhpCliPath">
/// The absolute path of the PHP command line binary of the website's PHP version, or <see langword="null"/> when the
/// website runs no PHP or the server did not report the path.
/// </param>
public sealed record ProvisionedWebsite(string? PhpCliPath);
