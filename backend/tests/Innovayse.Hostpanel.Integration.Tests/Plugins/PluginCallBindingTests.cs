using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginCallBinding"/>: only two shapes exist, a client with its reference and permissions, or no client,
/// no reference and no permissions; a blank plugin id or client reference is refused.
/// </summary>
public sealed class PluginCallBindingTests
{
    /// <summary>The plugin every binding in these tests is for.</summary>
    private const string PluginId = "binding-test";

    /// <summary>A client binding keeps the plugin, the client, its reference and the permissions it was given.</summary>
    [Fact]
    public void ForClient_KeepsTheValues()
    {
        var binding = PluginCallBinding.ForClient(PluginId, 42, "ref-42", ClientPermission.ViewDomains);

        binding.PluginId.Should().Be(PluginId);
        binding.ClientId.Should().Be(42);
        binding.ClientRef.Should().Be("ref-42");
        binding.Permissions.Should().Be(ClientPermission.ViewDomains);
    }

    /// <summary>A binding without a client has no client id, no reference and no permissions.</summary>
    [Fact]
    public void WithoutClient_HasNoClientNoReferenceAndNoPermissions()
    {
        var binding = PluginCallBinding.WithoutClient(PluginId);

        binding.PluginId.Should().Be(PluginId);
        binding.ClientId.Should().BeNull();
        binding.ClientRef.Should().BeNull();
        binding.Permissions.Should().Be(ClientPermission.None);
    }

    /// <summary>A client binding with an empty or white-space reference is refused, naming the reference.</summary>
    /// <param name="clientRef">The blank reference.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ForClient_BlankReference_IsRefused(string clientRef)
    {
        var build = () => PluginCallBinding.ForClient(PluginId, 42, clientRef, ClientPermission.All);

        build.Should().Throw<ArgumentException>().WithParameterName("clientRef");
    }

    /// <summary>A client binding with an empty or white-space plugin id is refused, naming the plugin id.</summary>
    /// <param name="pluginId">The blank plugin id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ForClient_BlankPluginId_IsRefused(string pluginId)
    {
        var build = () => PluginCallBinding.ForClient(pluginId, 42, "ref-42", ClientPermission.All);

        build.Should().Throw<ArgumentException>().WithParameterName("pluginId");
    }

    /// <summary>A binding without a client and with an empty or white-space plugin id is refused, naming the plugin id.</summary>
    /// <param name="pluginId">The blank plugin id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutClient_BlankPluginId_IsRefused(string pluginId)
    {
        var build = () => PluginCallBinding.WithoutClient(pluginId);

        build.Should().Throw<ArgumentException>().WithParameterName("pluginId");
    }
}
