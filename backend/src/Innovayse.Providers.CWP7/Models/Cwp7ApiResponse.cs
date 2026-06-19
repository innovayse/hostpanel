namespace Innovayse.Providers.CWP7.Models;

using System.Text.Json.Serialization;

/// <summary>Parsed response from the CWP7 REST API.</summary>
internal sealed class Cwp7ApiResponse
{
    /// <summary>Gets or initializes the result status — "OK" on success, "Error" on failure.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable message returned by CWP7.</summary>
    [JsonPropertyName("msj")]
    public string Msj { get; set; } = string.Empty;

    /// <summary>Gets the human-readable message (alias for <see cref="Msj"/>).</summary>
    [JsonIgnore]
    public string Message => Msj;

    /// <summary>Returns true when <see cref="Status"/> equals "OK" (case-insensitive).</summary>
    public bool IsSuccess =>
        string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase);
}
