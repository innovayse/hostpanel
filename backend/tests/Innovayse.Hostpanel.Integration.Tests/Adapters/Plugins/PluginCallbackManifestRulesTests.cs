using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rules for <c>api.callbacks</c>: the SDK's own check (no permission, no streaming) breaks
/// <see cref="PluginManifestRule.CallbackInvalid"/>, and a callback path and a duplicate callback break the same path and
/// duplicate rules routes have. No container or database.
/// </summary>
public sealed class PluginCallbackManifestRulesTests
{
    /// <summary>Reads a version 2 manifest with the given members after the required ones.</summary>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string extra)
    {
        return PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("callback-rules", ",\"manifestVersion\":2" + extra))!;
    }

    /// <summary>Asserts that a manifest breaks exactly one given rule.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="rule">The rule it must break.</param>
    private static void AssertBreaks(PluginManifest manifest, PluginManifestRule rule)
    {
        PluginManifestValidator.Validate(manifest).Should().NotBeNull().And.Match<PluginManifestViolation>(v => v.Rule == rule);
    }

    /// <summary>
    /// Well formed callbacks pass, and the same route may be both a callback and a client route, once the plugin declares the
    /// <c>endpoints</c> capability the client route needs.
    /// </summary>
    [Fact]
    public void Validate_WellFormedCallbacks_Pass()
    {
        PluginManifestValidator.Validate(Manifest(
            ""","capabilities":["endpoints"],"api":{"callbacks":[{"method":"post","path":"/pay/{order}"},{"method":"get","path":"/ping"}],"client":[{"method":"post","path":"/pay/{order}"}]}"""))
            .Should().BeNull();
    }

    /// <summary>A manifest with no callbacks, or a null list, passes.</summary>
    [Fact]
    public void Validate_NoCallbacks_Passes()
    {
        PluginManifestValidator.Validate(Manifest(""","api":{"callbacks":null}""")).Should().BeNull();
        PluginManifestValidator.Validate(Manifest("")).Should().BeNull();
    }

    /// <summary>A callback that declares a permission breaks the callback rule, whatever the permission.</summary>
    /// <param name="permission">The permission as written in the manifest.</param>
    [Theory]
    [InlineData("Any")]
    [InlineData("ViewDomains")]
    [InlineData("Nonsense")]
    public void Validate_CallbackWithPermission_BreaksCallbackInvalid(string permission)
    {
        AssertBreaks(
            Manifest($$$""","api":{"callbacks":[{"method":"post","path":"/pay","permission":"{{{permission}}}"}]}"""),
            PluginManifestRule.CallbackInvalid);
    }

    /// <summary>A streaming callback breaks the callback rule.</summary>
    [Fact]
    public void Validate_StreamingCallback_BreaksCallbackInvalid()
    {
        AssertBreaks(
            Manifest(""","api":{"callbacks":[{"method":"get","path":"/events","streaming":true}]}"""), PluginManifestRule.CallbackInvalid);
    }

    /// <summary>A callback path that is not lowercase segments and parameters breaks the path rule.</summary>
    /// <param name="path">The path as written in the manifest.</param>
    [Theory]
    [InlineData("pay")]
    [InlineData("/Pay")]
    [InlineData("/a/../b")]
    [InlineData("/a//b")]
    [InlineData("/")]
    public void Validate_CallbackPathInvalid_BreaksRoutePathInvalid(string path)
    {
        AssertBreaks(
            Manifest($$$""","api":{"callbacks":[{"method":"post","path":"{{{path}}}"}]}"""), PluginManifestRule.RoutePathInvalid);
    }

    /// <summary>The same method and path template twice among the callbacks breaks the duplicate rule, parameter names aside.</summary>
    [Fact]
    public void Validate_DuplicateCallback_BreaksRouteDuplicate()
    {
        AssertBreaks(
            Manifest(""","api":{"callbacks":[{"method":"post","path":"/pay/{order}"},{"method":"post","path":"/pay/{id}"}]}"""),
            PluginManifestRule.RouteDuplicate);
    }

    /// <summary>The callback rule maps to its own error code.</summary>
    [Fact]
    public void CodeFor_CallbackInvalid_IsPluginCallbackInvalid()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.CallbackInvalid).Should().Be(HostpanelErrorCodes.PLUGIN_CALLBACK_INVALID);
    }
}
