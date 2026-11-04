using System.Text.Json.Serialization;
using Hostpanel.Sdk.Plugins.HostingPanel;

namespace Innovayse.Providers.CWP7.Models;

#pragma warning disable CS0618 // Legacy HostingPanel contract, obsolete since SDK 3.1.0; stays until the CWP providers become IProvisioningPanelPlugin plugins.
/// <summary>Parsed response from the CWP7 <c>/v1/user_session</c> list action.</summary>
/// <remarks>
/// Its own model rather than <see cref="HostingPanelApiWire"/>: this endpoint answers with an object
/// under <c>msj</c> where every other one answers with a string, so deserializing it into the
/// shared response throws and the call reads as a failure.
/// </remarks>
internal sealed class Cwp7UserSessionWire
{
    /// <summary>Gets or initializes the result status: "OK" on success.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the payload carrying one entry per requested account.</summary>
    [JsonPropertyName("msj")]
    public Cwp7UserSessionPayload? Payload { get; init; }
}
#pragma warning restore CS0618
