namespace Innovayse.Providers.CWP.Models;

using System.Text.Json.Serialization;

/// <summary>Parsed response from the CWP /v1/version endpoint.</summary>
internal sealed class CwpVersionResponse
{
    /// <summary>Gets or initializes the result status — "OK" on success.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the CWP version string returned by the server.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

/// <summary>Parsed response from the CWP /v1/account list action.</summary>
internal sealed class CwpAccountListResponse
{
    /// <summary>Gets or initializes the result status — "OK" on success.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the list of account entries.</summary>
    [JsonPropertyName("msj")]
    public List<object>? Accounts { get; init; }
}
