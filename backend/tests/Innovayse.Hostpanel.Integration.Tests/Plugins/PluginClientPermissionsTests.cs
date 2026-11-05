using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>How a declared <c>permission</c> member is read: only a defined name or <c>Any</c> relaxes the default, and every other value fails closed.</summary>
public sealed class PluginClientPermissionsTests
{
    /// <summary>A number or a list of names never parses as a permission, so the entry stays for the account holder alone.</summary>
    /// <param name="declared">The declared member.</param>
    [Theory]
    [InlineData("7")]
    [InlineData("A, B")]
    [InlineData("ViewDomains, ViewEmails")]
    [InlineData("")]
    [InlineData(null)]
    public void ANumberOrAList_FailsClosedToTheAccountHolder(string? declared)
    {
        PluginClientPermissions.Required(declared).Should().Be(ClientPermission.All);
        PluginClientPermissions.IsHeld(ClientPermission.ViewDomains, declared).Should().BeFalse();
    }

    /// <summary><c>Any</c> needs nothing, and a defined name needs exactly that flag.</summary>
    [Fact]
    public void AnyAndADefinedName_AreReadAsDeclared()
    {
        PluginClientPermissions.Required(PluginPermissions.Any).Should().Be(ClientPermission.None);
        PluginClientPermissions.Required(nameof(ClientPermission.ViewDomains)).Should().Be(ClientPermission.ViewDomains);
        PluginClientPermissions.IsHeld(ClientPermission.None, PluginPermissions.Any).Should().BeTrue();
    }
}
