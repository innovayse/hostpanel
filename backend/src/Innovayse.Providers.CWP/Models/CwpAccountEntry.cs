using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Plugins.HostingPanel;

namespace Innovayse.Providers.CWP.Models;

/// <summary>One account in the CWP <c>/v1/account</c> list answer.</summary>
internal sealed class CwpAccountEntry
{
    /// <summary>Gets or initializes the account username.</summary>
    [JsonPropertyName("user")]
    public string? User { get; init; }

    /// <summary>Gets or initializes the account's primary domain.</summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    /// <summary>Gets or initializes the raw suspension flag, which CWP writes as a number, a string or a boolean.</summary>
    [JsonPropertyName("suspended")]
    public JsonElement Suspended { get; init; }

    /// <summary>Gets a value indicating whether the account is currently suspended; an absent flag reads as active.</summary>
    [JsonIgnore]
    public bool IsSuspended => HostingPanelApiReader.IsFlagSet(Suspended);
}
