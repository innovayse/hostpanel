using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Vat;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The VAT number port is the one seam to the registers: handlers ask <see cref="IVatNumberChecker"/> and never learn which
/// service answered, and the adapter of each register stays behind the one router that picks it.
/// </summary>
public sealed class VatNumberCheckerPortTests
{
    /// <summary>The port is declared where every port is, and the scan counts it as one.</summary>
    [Fact]
    public void ThePort_IsAPortInCommonInterfaces()
    {
        PortPlacementTests.Ports().Should().Contain(typeof(IVatNumberChecker));
    }

    /// <summary>The port is implemented in Infrastructure only, by the router and the demo's stand-in, and by nothing else.</summary>
    [Fact]
    public void ThePort_IsImplementedOnlyByInfrastructureAdapters()
    {
        var implementations = new[]
            {
                Layers.Application, Layers.Shared, Layers.Api, Layers.Infrastructure,
            }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IVatNumberChecker).IsAssignableFrom(type))
            .ToList();

        implementations.Should().BeEquivalentTo([typeof(VatNumberChecker), typeof(DemoVatNumberChecker)]);
    }

    /// <summary>
    /// The adapter of one register is built only by the router: no other type of the application takes the EU or the UK
    /// client, so no handler or service can pick a register itself.
    /// </summary>
    [Fact]
    public void TheRegisterClients_AreTakenOnlyByTheRouter()
    {
        var registerClients = new[] { typeof(ViesVatClient), typeof(HmrcVatClient) };

        var takers = new[]
            {
                Layers.Application, Layers.Shared, Layers.Api, Layers.Infrastructure,
            }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => type.GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => registerClients.Contains(parameter.ParameterType))))
            .ToList();

        takers.Should().BeEquivalentTo([typeof(VatNumberChecker)]);
    }
}
