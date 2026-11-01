using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// A refused signature picks its own error code: every <see cref="PluginSignatureRule"/> maps to one, a
/// <see cref="PluginPackageOutcome.SignatureRefused"/> result without its rule is a programming error, and
/// <c>ResourceCoverageTests</c> checks that each code has its text in every language.
/// </summary>
public sealed class PluginSignatureErrorsTests
{
    /// <summary>Each signature rule answers with its own code.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="code">The code it answers with.</param>
    [Theory]
    [InlineData(PluginSignatureRule.Unsigned, "PLUGIN_UNSIGNED")]
    [InlineData(PluginSignatureRule.Malformed, "PLUGIN_SIGNATURE_MALFORMED")]
    [InlineData(PluginSignatureRule.UnknownKey, "PLUGIN_SIGNATURE_KEY_UNKNOWN")]
    [InlineData(PluginSignatureRule.Invalid, "PLUGIN_SIGNATURE_INVALID")]
    public void EachRule_HasItsOwnCode(PluginSignatureRule rule, string code)
    {
        PluginPackageErrors.CodeFor(rule).Should().Be(code);
        PluginPackageErrors.CodeFor(new PluginPackageResult(PluginPackageOutcome.SignatureRefused, "p", null, SignatureRule: rule))
            .Should().Be(code);
    }

    /// <summary>Every defined rule is covered above, so a new rule cannot be added without a code.</summary>
    [Fact]
    public void EveryDefinedRule_HasACode()
    {
        foreach (var rule in Enum.GetValues<PluginSignatureRule>())
        {
            PluginPackageErrors.CodeFor(rule).Should().StartWith("PLUGIN_");
        }
    }

    /// <summary>A refused-signature result that does not name its rule cannot be turned into a code.</summary>
    [Fact]
    public void ARefusedResultWithoutItsRule_IsRejected()
    {
        var code = () => PluginPackageErrors.CodeFor(new PluginPackageResult(PluginPackageOutcome.SignatureRefused, "p", null));

        code.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>The new codes are the constants the API answers with.</summary>
    [Fact]
    public void TheCodes_AreTheErrorCodeConstants()
    {
        PluginPackageErrors.CodeFor(PluginSignatureRule.Unsigned).Should().Be(HostpanelErrorCodes.PLUGIN_UNSIGNED);
        PluginPackageErrors.CodeFor(PluginSignatureRule.Malformed).Should().Be(HostpanelErrorCodes.PLUGIN_SIGNATURE_MALFORMED);
        PluginPackageErrors.CodeFor(PluginSignatureRule.UnknownKey).Should().Be(HostpanelErrorCodes.PLUGIN_SIGNATURE_KEY_UNKNOWN);
        PluginPackageErrors.CodeFor(PluginSignatureRule.Invalid).Should().Be(HostpanelErrorCodes.PLUGIN_SIGNATURE_INVALID);
    }
}
