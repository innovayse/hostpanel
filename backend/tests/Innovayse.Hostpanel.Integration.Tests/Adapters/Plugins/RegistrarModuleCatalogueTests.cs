using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="RegistrarModuleCatalogue"/>: which registrar plugins are installed and what each offers, read from the manifests,
/// with a stored module matched to a plugin id without regard to letter case.
/// </summary>
public sealed class RegistrarModuleCatalogueTests
{
    /// <summary>Builds the catalogue over a registry holding a full registrar, an operations only registrar and a payment plugin.</summary>
    /// <returns>The catalogue under test.</returns>
    private static RegistrarModuleCatalogue Catalogue()
    {
        return new RegistrarModuleCatalogue(PluginActivationTestSeeds.Registry(
            RegistrarRegistryTestSeeds.FullRegistrar("full-registrar"),
            RegistrarRegistryTestSeeds.OperationsOnlyRegistrar("basic-registrar"),
            RegistrarRegistryTestSeeds.Plugin("a-gateway", PluginType.Payment, typeof(ScriptedRegistrarPlugin))));
    }

    /// <summary>An installed registrar is found by its id in any letter case and answers with the id the plugin declares.</summary>
    [Fact]
    public void FindId_InstalledRegistrar_IsFoundWithoutRegardToLetterCase()
    {
        var catalogue = Catalogue();

        catalogue.FindId("full-registrar").Should().Be("full-registrar");
        catalogue.FindId("FULL-Registrar").Should().Be("full-registrar");
        catalogue.FindId(" basic-registrar ").Should().Be("basic-registrar");
    }

    /// <summary>An unknown module and a plugin that is not a registrar are not registrars.</summary>
    [Fact]
    public void FindId_UnknownOrNotARegistrar_IsNull()
    {
        var catalogue = Catalogue();

        catalogue.FindId("no-such-registrar").Should().BeNull();
        catalogue.FindId("a-gateway").Should().BeNull();
    }

    /// <summary>The capabilities follow what each manifest declares, and a module that names no registrar offers nothing.</summary>
    [Fact]
    public void CapabilitiesOf_FollowTheManifest()
    {
        var catalogue = Catalogue();

        catalogue.CapabilitiesOf("Full-Registrar").Should().Be(new RegistrarCapabilitiesDto(true, true, true));
        catalogue.CapabilitiesOf("basic-registrar").Should().Be(new RegistrarCapabilitiesDto(false, false, false));
        catalogue.CapabilitiesOf("no-such-registrar").Should().Be(new RegistrarCapabilitiesDto(false, false, false));
        catalogue.CapabilitiesOf(null).Should().Be(new RegistrarCapabilitiesDto(false, false, false));
    }
}
