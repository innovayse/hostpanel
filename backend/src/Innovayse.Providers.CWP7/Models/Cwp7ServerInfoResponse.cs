namespace Innovayse.Providers.CWP7.Models;

using System.Text.Json.Serialization;

/// <summary>Parsed response from the CWP7 /v1/packages list action, used to verify connectivity.</summary>
internal sealed class Cwp7PackageListResponse
{
    /// <summary>Gets or initializes the result status — "OK" on success.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the list of package entries.</summary>
    [JsonPropertyName("msj")]
    public List<object>? Packages { get; init; }
}
