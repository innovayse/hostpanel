using Innovayse.Hostpanel.Infrastructure;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// <see cref="ProvisioningOptionsValidator"/> and the <c>AddProvisioningOptions</c>
/// registration the application uses: both cPanel ports must be TCP ports, and a bad value stops the host on start.
/// </summary>
public sealed class ProvisioningOptionsValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly ProvisioningOptionsValidator _validator = new();

    /// <summary>The defaults, and the ends of the range, are valid.</summary>
    /// <param name="whm">The WHM port.</param>
    /// <param name="user">The account owner's port.</param>
    [Theory]
    [InlineData(ProvisioningOptions.DefaultCPanelWhmPort, ProvisioningOptions.DefaultCPanelUserPort)]
    [InlineData(1, 65535)]
    [InlineData(65535, 1)]
    public void Validate_PortsInRange_Succeeds(int whm, int user)
    {
        var result = _validator.Validate(null, new ProvisioningOptions { CPanelWhmPort = whm, CPanelUserPort = user });

        result.Succeeded.Should().BeTrue();
    }

    /// <summary>A WHM port outside 1 to 65535 is refused, naming its key.</summary>
    /// <param name="port">The port.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    public void Validate_WhmPortOutOfRange_FailsNamingTheKey(int port)
    {
        var result = _validator.Validate(null, new ProvisioningOptions { CPanelWhmPort = port });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Contain("Provisioning:CPanelWhmPort").And.Contain("Provisioning__CPanelWhmPort");
    }

    /// <summary>An account owner port outside 1 to 65535 is refused, naming its key.</summary>
    /// <param name="port">The port.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(70000)]
    public void Validate_UserPortOutOfRange_FailsNamingTheKey(int port)
    {
        var result = _validator.Validate(null, new ProvisioningOptions { CPanelUserPort = port });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Contain("Provisioning:CPanelUserPort");
    }

    /// <summary>Both bad ports are reported together.</summary>
    [Fact]
    public void Validate_BothPortsBad_ReportsBoth()
    {
        var result = _validator.Validate(null, new ProvisioningOptions { CPanelWhmPort = 0, CPanelUserPort = 0 });

        result.Failures.Should().HaveCount(2);
    }

    /// <summary>The real registration stops startup on a bad port setting.</summary>
    [Fact]
    public void Startup_WithBadWhmPort_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Provisioning:CPanelWhmPort"] = "99999" });

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Provisioning:CPanelWhmPort");
    }

    /// <summary>The real registration stops startup on a port that is zero.</summary>
    [Fact]
    public void Startup_WithZeroUserPort_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Provisioning:CPanelUserPort"] = "0" });

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Provisioning:CPanelUserPort");
    }

    /// <summary>The real registration accepts a configuration without the section, and one with valid ports.</summary>
    [Fact]
    public void Startup_WithDefaultsOrValidPorts_Succeeds()
    {
        using var defaults = BuildProvider([]);
        using var custom = BuildProvider(new Dictionary<string, string?> { ["Provisioning:CPanelWhmPort"] = "2188", ["Provisioning:CPanelUserPort"] = "2183" });

        var withDefaults = () => defaults.GetRequiredService<IStartupValidator>().Validate();
        var withCustom = () => custom.GetRequiredService<IStartupValidator>().Validate();

        withDefaults.Should().NotThrow();
        withCustom.Should().NotThrow();
        custom.GetRequiredService<IOptions<ProvisioningOptions>>().Value.CPanelWhmPort.Should().Be(2188);
    }

    /// <summary>Builds a container with the real registration over the given settings.</summary>
    /// <param name="settings">The configuration values.</param>
    /// <returns>The built provider.</returns>
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddProvisioningOptions(configuration).BuildServiceProvider();
    }
}
