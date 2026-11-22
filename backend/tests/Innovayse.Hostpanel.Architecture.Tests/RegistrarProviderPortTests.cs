using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The registrar port is the one seam to a registrar: handlers ask <see cref="IRegistrarProvider"/> and never learn which plugin
/// answered, and the SDK's registrar contract is called from one adapter only.
/// </summary>
public sealed class RegistrarProviderPortTests
{
    /// <summary>Every authored type outside the SDK and the tests.</summary>
    /// <returns>The authored types of every shipped layer.</returns>
    private static IEnumerable<Type> AuthoredTypes()
    {
        return new[] { Layers.Application, Layers.Shared, Layers.Api, Layers.Infrastructure }.SelectMany(Layers.AuthoredTypesOf);
    }

    /// <summary>The port is declared where every port is, and the scan counts it as one.</summary>
    [Fact]
    public void ThePorts_ArePortsInCommonInterfaces()
    {
        var ports = PortPlacementTests.Ports();

        ports.Should().Contain(typeof(IRegistrarProvider));
        ports.Should().Contain(typeof(IRegistrarProviderFactory));
        ports.Should().Contain(typeof(IRegistrarModuleCatalogue));
    }

    /// <summary>The provider port is implemented by the plugin adapter, the refusing provider and the demo's stand-in, and by nothing else.</summary>
    [Fact]
    public void TheProviderPort_IsImplementedOnlyByThePluginAdapterTheRefusingProviderAndTheDemo()
    {
        var implementations = AuthoredTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IRegistrarProvider).IsAssignableFrom(type))
            .ToList();

        implementations.Should().BeEquivalentTo([typeof(PluginRegistrarProvider), typeof(UnavailableRegistrarProvider), typeof(DemoRegistrarProvider)]);
    }

    /// <summary>The SDK's registrar operations contract is named only inside <c>Infrastructure/Plugins</c>, so one adapter calls it.</summary>
    [Fact]
    public void TheSdkOperationsContract_IsReferencedOnlyInsideInfrastructurePlugins()
    {
        var outsiders = AuthoredTypes()
            .Where(type => type.Namespace?.StartsWith("Innovayse.Hostpanel.Infrastructure.Plugins", StringComparison.Ordinal) != true)
            .Where(type => type.GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => typeof(IRegistrarOperationsPlugin).IsAssignableFrom(parameter.ParameterType)))
                || type.GetMethods().Any(method => typeof(IRegistrarOperationsPlugin).IsAssignableFrom(method.ReturnType)
                    || method.GetParameters().Any(parameter => typeof(IRegistrarOperationsPlugin).IsAssignableFrom(parameter.ParameterType))))
            .ToList();

        outsiders.Should().BeEmpty();
    }
}
