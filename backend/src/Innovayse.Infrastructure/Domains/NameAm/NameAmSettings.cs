namespace Innovayse.Infrastructure.Domains.NameAm;

/// <summary>
/// Configuration settings for the Name.am registrar API integration.
/// Bind from <c>appsettings.json</c> section <c>NameAm</c>.
/// </summary>
public sealed class NameAmSettings
{
    /// <summary>Gets the Name.am account email address used for authentication.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Gets the Name.am account password used for authentication.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Gets the Name.am API base URL.</summary>
    public string ApiUrl { get; init; } = "https://api.name.am";
}
