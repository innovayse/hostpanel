using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// A registrar plugin whose connection test answers what its own saved setting <see cref="OutcomeKey"/> says, so a test that
/// reaches the plugin only through the activator can still choose the answer: a <see cref="PanelConnectionOutcome"/> name, or
/// <see cref="ThrowValue"/> to make the test throw.
/// </summary>
public sealed class ConfiguredOutcomeRegistrarPlugin : ScriptedRegistrarPlugin
{
    /// <summary>The setting key the outcome is saved under.</summary>
    public const string OutcomeKey = "connection_outcome";

    /// <summary>The setting value that makes the connection test throw.</summary>
    public const string ThrowValue = "Throw";

    /// <summary>Initializes a new instance of the <see cref="ConfiguredOutcomeRegistrarPlugin"/> class.</summary>
    /// <param name="configuration">The configuration composed for this plugin alone, holding the saved outcome.</param>
    public ConfiguredOutcomeRegistrarPlugin(IConfiguration configuration)
        : base(configuration)
    {
        var saved = configuration.AsEnumerable()
            .FirstOrDefault(entry => entry.Key.EndsWith(":" + OutcomeKey, StringComparison.OrdinalIgnoreCase)).Value;

        if (saved == ThrowValue)
        {
            Throws = new InvalidOperationException("The connection test failed.");
        }
        else if (Enum.TryParse<PanelConnectionOutcome>(saved, out var outcome))
        {
            ConnectionTest = outcome == PanelConnectionOutcome.Connected
                ? PanelConnectionTestResult.Connected(null, null)
                : PanelConnectionTestResult.Failed(outcome);
        }
    }
}
