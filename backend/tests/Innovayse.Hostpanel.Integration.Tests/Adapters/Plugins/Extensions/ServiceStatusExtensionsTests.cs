using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins.Extensions;

/// <summary>
/// <see cref="ServiceStatusExtensions"/>: every host service status maps to the SDK status of the same name (the numbers
/// differ, so a cast would be wrong), and a value the mapping does not know fails loudly.
/// </summary>
public sealed class ServiceStatusExtensionsTests
{
    /// <summary>Every service status maps to the plugin status with the same name.</summary>
    [Fact]
    public void ServiceStatuses_MapByName()
    {
        foreach (var status in Enum.GetValues<ServiceStatus>())
        {
            status.ToPlugin().ToString().Should().Be(status.ToString());
        }
    }

    /// <summary>Every plugin status is the target of exactly one host service status, so none is unreachable by accident.</summary>
    [Fact]
    public void ServiceMapping_IsInjective()
    {
        var statuses = Enum.GetValues<ServiceStatus>();

        statuses.Select(s => s.ToPlugin()).Distinct().Should().HaveCount(statuses.Length);
    }

    /// <summary>An undefined service status throws instead of being passed on.</summary>
    [Fact]
    public void UndefinedServiceStatus_Throws()
    {
        var act = () => ((ServiceStatus)999).ToPlugin();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
