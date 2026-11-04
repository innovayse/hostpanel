using Innovayse.Hostpanel.Domain.Plugins;

namespace Innovayse.Hostpanel.Domain.Tests.Plugins;

/// <summary>The rules a <see cref="PluginSlotProductBinding"/> row is created under.</summary>
public sealed class PluginSlotProductBindingTests
{
    /// <summary>A valid binding keeps what it was created with.</summary>
    [Fact]
    public void Create_ValidIds_KeepsThem()
    {
        var binding = PluginSlotProductBinding.Create("acme-usage", "admin.service.panels", "usage", 7);

        binding.PluginId.Should().Be("acme-usage");
        binding.SlotId.Should().Be("admin.service.panels");
        binding.WidgetId.Should().Be("usage");
        binding.ProductId.Should().Be(7);
    }

    /// <summary>A plugin id that breaks the plugin id rule, an empty or oversized id and a non-positive product are refused.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="slotId">The slot id.</param>
    /// <param name="widgetId">The widget id.</param>
    /// <param name="productId">The product id.</param>
    [Theory]
    [InlineData("Bad Plugin", "slot", "widget", 1)]
    [InlineData("acme", "", "widget", 1)]
    [InlineData("acme", "slot", " ", 1)]
    [InlineData("acme", "slot", "widget", 0)]
    [InlineData("acme", "slot", "widget", -3)]
    public void Create_InvalidInput_Throws(string pluginId, string slotId, string widgetId, int productId)
    {
        var create = () => PluginSlotProductBinding.Create(pluginId, slotId, widgetId, productId);

        create.Should().Throw<ArgumentException>();
    }

    /// <summary>An id longer than the column is refused, one at the limit is accepted.</summary>
    [Fact]
    public void IsValidId_AcceptsUpToTheColumnLength()
    {
        PluginSlotProductBinding.IsValidId(new string('a', PluginSlotProductBinding.IdMaxLength)).Should().BeTrue();
        PluginSlotProductBinding.IsValidId(new string('a', PluginSlotProductBinding.IdMaxLength + 1)).Should().BeFalse();
        PluginSlotProductBinding.IsValidId(null).Should().BeFalse();
    }
}
