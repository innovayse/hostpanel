using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginFactMapper"/>: every host status maps to the SDK status of the same name (the numbers differ, so a
/// cast would be wrong), and a value the mapper does not know fails loudly.
/// </summary>
public sealed class PluginFactMapperTests
{
    /// <summary>Every service status maps to the plugin status with the same name.</summary>
    [Fact]
    public void ServiceStatuses_MapByName()
    {
        foreach (var status in Enum.GetValues<ServiceStatus>())
        {
            PluginFactMapper.ToPlugin(status).ToString().Should().Be(status.ToString());
        }
    }

    /// <summary>Every invoice status maps to the plugin status with the same name.</summary>
    [Fact]
    public void InvoiceStatuses_MapByName()
    {
        foreach (var status in Enum.GetValues<InvoiceStatus>())
        {
            PluginFactMapper.ToPlugin(status).ToString().Should().Be(status.ToString());
        }
    }

    /// <summary>Every plugin status is the target of exactly one host status, so none is unreachable by accident.</summary>
    [Fact]
    public void ServiceMapping_IsInjective()
    {
        Enum.GetValues<ServiceStatus>().Select(PluginFactMapper.ToPlugin).Distinct().Should().HaveCount(Enum.GetValues<ServiceStatus>().Length);
        Enum.GetValues<InvoiceStatus>().Select(PluginFactMapper.ToPlugin).Distinct().Should().HaveCount(Enum.GetValues<InvoiceStatus>().Length);
    }

    /// <summary>An undefined service status throws instead of being passed on.</summary>
    [Fact]
    public void UndefinedServiceStatus_Throws()
    {
        var act = () => PluginFactMapper.ToPlugin((ServiceStatus)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>An undefined invoice status throws instead of being passed on.</summary>
    [Fact]
    public void UndefinedInvoiceStatus_Throws()
    {
        var act = () => PluginFactMapper.ToPlugin((InvoiceStatus)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
