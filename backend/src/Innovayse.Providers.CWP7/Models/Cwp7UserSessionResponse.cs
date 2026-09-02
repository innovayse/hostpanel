namespace Innovayse.Providers.CWP7.Models;

using System.Text.Json.Serialization;

/// <summary>Parsed response from the CWP7 <c>/v1/user_session</c> list action.</summary>
/// <remarks>
/// Its own model rather than <see cref="Cwp7ApiResponse"/>: this endpoint answers with an object
/// under <c>msj</c> where every other one answers with a string, so deserializing it into the
/// shared response throws and the call reads as a failure.
/// </remarks>
internal sealed class Cwp7UserSessionResponse
{
    /// <summary>Gets or initializes the result status — "OK" on success.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the payload carrying one entry per requested account.</summary>
    [JsonPropertyName("msj")]
    public Cwp7UserSessionPayload? Payload { get; init; }
}

/// <summary>The <c>msj</c> object of a CWP7 user-session response.</summary>
internal sealed class Cwp7UserSessionPayload
{
    /// <summary>Gets or initializes how many accounts CWP7 matched.</summary>
    [JsonPropertyName("accounts")]
    public int Accounts { get; init; }

    /// <summary>Gets or initializes one session entry per matched account.</summary>
    [JsonPropertyName("details")]
    public List<Cwp7UserSessionDetail>? Details { get; init; }
}

/// <summary>One account's freshly minted control-panel session.</summary>
internal sealed class Cwp7UserSessionDetail
{
    /// <summary>Gets or initializes the account username the session belongs to.</summary>
    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;

    /// <summary>Gets or initializes the opaque session token.</summary>
    /// <remarks>Carried inside <see cref="Url"/> already; kept because CWP7 sends it.</remarks>
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the ready-to-open control-panel URL with the session token in it.
    /// </summary>
    /// <remarks>
    /// Used as-is rather than rebuilt from <see cref="Token"/>: CWP7 composes it against its own
    /// hostname and port, which need not be the address the API was called on.
    /// </remarks>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
