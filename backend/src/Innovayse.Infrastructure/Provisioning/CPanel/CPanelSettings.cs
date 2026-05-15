namespace Innovayse.Infrastructure.Provisioning.CPanel;

/// <summary>cPanel WHM API configuration settings.</summary>
public sealed class CPanelSettings
{
    /// <summary>Gets the WHM API URL (e.g. https://server.example.com:2087).</summary>
    public required string ApiUrl { get; init; }

    /// <summary>Gets the WHM username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the WHM API token.</summary>
    public required string ApiToken { get; init; }

    /// <summary>Gets the server IP address.</summary>
    public required string ServerIp { get; init; }
}
