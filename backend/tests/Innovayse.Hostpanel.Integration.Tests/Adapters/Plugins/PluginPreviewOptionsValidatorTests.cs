using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.Hosting.Internal;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginPreviewOptionsValidator"/> keeps plain http preview origins off every host but a Development one, and
/// to loopback origins there.
/// </summary>
public sealed class PluginPreviewOptionsValidatorTests
{
    /// <summary>Validates a list of origins under an environment.</summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="origins">The configured origins.</param>
    /// <returns>Whether the options pass.</returns>
    private static bool Passes(string environment, params string[] origins)
    {
        return new PluginPreviewOptionsValidator(new HostingEnvironment { EnvironmentName = environment })
            .Validate(null, new PluginPreviewOptions { DevelopmentHttpOrigins = origins })
            .Succeeded;
    }

    /// <summary>An empty list, the default, passes everywhere.</summary>
    /// <param name="environment">The environment name.</param>
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Testing")]
    public void EmptyList_Passes(string environment)
    {
        Passes(environment).Should().BeTrue();
    }

    /// <summary>A blank entry, what the dev compose file passes when no origin is chosen, counts as unset everywhere.</summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="origin">The blank entry.</param>
    [Theory]
    [InlineData("Development", "")]
    [InlineData("Development", " ")]
    [InlineData("Production", "")]
    [InlineData("Testing", " ")]
    public void BlankEntry_CountsAsUnset(string environment, string origin)
    {
        Passes(environment, origin).Should().BeTrue();
    }

    /// <summary>A loopback origin passes in Development only: production refuses to start.</summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="expected">Whether it passes.</param>
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("Testing", false)]
    public void LoopbackOrigin_PassesOnlyInDevelopment(string environment, bool expected)
    {
        Passes(environment, "http://localhost:8088").Should().Be(expected);
    }

    /// <summary>Even in Development an origin must be plain http on a loopback host.</summary>
    /// <param name="origin">The configured origin.</param>
    [Theory]
    [InlineData("http://example.org")]
    [InlineData("https://localhost:8088")]
    [InlineData("http://localhost:8088/x")]
    public void NotALoopbackHttpOrigin_FailsInDevelopment(string origin)
    {
        Passes("Development", origin).Should().BeFalse();
    }
}
