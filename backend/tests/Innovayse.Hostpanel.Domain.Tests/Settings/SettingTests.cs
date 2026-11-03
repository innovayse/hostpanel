using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>Unit tests for the <see cref="Setting"/> aggregate.</summary>
public sealed class SettingTests
{
    /// <summary>A plugin id that meets the plugin id rule.</summary>
    private const string PluginId = "acme-reports";

    /// <summary>The moment every removal in these tests is stamped with.</summary>
    private static readonly DateTimeOffset RemovedAt = new(2026, 11, 2, 10, 30, 0, TimeSpan.Zero);

    /// <summary>Create keeps the key, value and description.</summary>
    [Fact]
    public void Create_KeepsFields()
    {
        var setting = Setting.Create("site:name", "Panel", "Site name.");

        setting.Key.Should().Be("site:name");
        setting.Value.Should().Be("Panel");
        setting.Description.Should().Be("Site name.");
    }

    /// <summary>UpdateValue replaces the value and leaves the key alone.</summary>
    [Fact]
    public void UpdateValue_ReplacesValue()
    {
        var setting = Setting.Create("site:name", "Panel", null);

        setting.UpdateValue("New panel");

        setting.Value.Should().Be("New panel");
        setting.Key.Should().Be("site:name");
    }

    /// <summary>Switching off an enabled plugin's switch stores the off value and announces nothing: the data is kept.</summary>
    [Fact]
    public void SwitchOffRemovedPlugin_OnTheSwitch_StoresOffAndKeepsTheData()
    {
        var setting = Setting.Create(
            IntegrationSettingKeys.EnabledKey(PluginId), IntegrationSettingKeys.EnabledValue, null);

        setting.SwitchOffRemovedPlugin(PluginId);

        setting.Value.Should().Be(IntegrationSettingKeys.DisabledValue);
        setting.DomainEvents.Should().BeEmpty();
    }

    /// <summary>A row that is not the plugin's switch is refused and not changed.</summary>
    [Fact]
    public void SwitchOffRemovedPlugin_OnAnotherSetting_ThrowsAndChangesNothing()
    {
        var setting = Setting.Create(
            IntegrationSettingKeys.EnabledKey("other-plugin"), IntegrationSettingKeys.EnabledValue, null);

        var act = () => setting.SwitchOffRemovedPlugin(PluginId);

        act.Should().Throw<ArgumentException>();
        setting.Value.Should().Be(IntegrationSettingKeys.EnabledValue);
    }

    /// <summary>An id breaking the plugin id rule is refused, even on a row whose key it would match.</summary>
    [Fact]
    public void SwitchOffRemovedPlugin_InvalidId_Throws()
    {
        const string invalidId = "Not_Valid";
        var setting = Setting.Create(
            IntegrationSettingKeys.EnabledKey(invalidId), IntegrationSettingKeys.EnabledValue, null);

        var act = () => setting.SwitchOffRemovedPlugin(invalidId);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>The factory for a plugin that never had a switch row builds that row already off, keyed as the activator reads it.</summary>
    [Fact]
    public void CreateSwitchedOffForRemovedPlugin_BuildsTheOffSwitch()
    {
        var setting = Setting.CreateSwitchedOffForRemovedPlugin(PluginId);

        setting.Key.Should().Be(IntegrationSettingKeys.EnabledKey(PluginId));
        setting.Value.Should().Be(IntegrationSettingKeys.DisabledValue);
        setting.Description.Should().Contain(PluginId);
        setting.DomainEvents.Should().BeEmpty();
    }

    /// <summary>The factory refuses an id breaking the plugin id rule, so no key can be built from it.</summary>
    [Fact]
    public void CreateSwitchedOffForRemovedPlugin_InvalidId_Throws()
    {
        var act = () => Setting.CreateSwitchedOffForRemovedPlugin("../outside");

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A purge request raises exactly one event naming the plugin and the moment, and leaves the value alone.</summary>
    [Fact]
    public void RequestDataPurge_OnTheSwitch_RaisesThePurgeEvent()
    {
        var setting = Setting.Create(
            IntegrationSettingKeys.EnabledKey(PluginId), IntegrationSettingKeys.DisabledValue, null);

        setting.RequestDataPurge(PluginId, RemovedAt);

        setting.Value.Should().Be(IntegrationSettingKeys.DisabledValue);
        setting.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new PluginDataPurgeRequestedEvent(PluginId, RemovedAt));
    }

    /// <summary>A purge request on another setting is refused and announces nothing.</summary>
    [Fact]
    public void RequestDataPurge_OnAnotherSetting_ThrowsAndRaisesNothing()
    {
        var setting = Setting.Create(
            IntegrationSettingKeys.EnabledKey("other-plugin"), IntegrationSettingKeys.DisabledValue, null);

        var act = () => setting.RequestDataPurge(PluginId, RemovedAt);

        act.Should().Throw<ArgumentException>();
        setting.DomainEvents.Should().BeEmpty();
    }
}
