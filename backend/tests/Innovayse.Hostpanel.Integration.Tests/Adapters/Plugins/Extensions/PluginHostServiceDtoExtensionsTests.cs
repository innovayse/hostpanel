using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins.Extensions;

/// <summary><see cref="PluginHostServiceDtoExtensions"/>: a service row reaches a plugin with every value unchanged.</summary>
public sealed class PluginHostServiceDtoExtensionsTests
{
    /// <summary>Every column is copied and the status is translated by name.</summary>
    [Fact]
    public void ToPlugin_CopiesEveryValue()
    {
        var created = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var renews = created.AddMonths(1);
        var dto = new PluginHostServiceDto(7, 11, ServiceStatus.Suspended, "example.test", created, renews);

        var result = dto.ToPlugin();

        result.Should().Be(new PluginClientService(7, 11, PluginServiceStatus.Suspended, "example.test", created, renews));
    }

    /// <summary>A service without a domain or a renewal keeps both as null.</summary>
    [Fact]
    public void ToPlugin_KeepsNulls()
    {
        var dto = new PluginHostServiceDto(1, 2, ServiceStatus.Pending, null, DateTimeOffset.UnixEpoch, null);

        var result = dto.ToPlugin();

        result.Domain.Should().BeNull();
        result.NextRenewalAt.Should().BeNull();
    }

    /// <summary>A service whose status has no plugin equivalent throws instead of being passed on.</summary>
    [Fact]
    public void ToPlugin_UndefinedStatus_Throws()
    {
        var dto = new PluginHostServiceDto(1, 2, (ServiceStatus)999, null, DateTimeOffset.UnixEpoch, null);

        var act = () => dto.ToPlugin();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
