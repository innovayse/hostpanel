using Innovayse.Hostpanel.Application.Admin.Integrations.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>Pins the single integration catalogue the list, detail and connection-test use cases read.</summary>
public sealed class IntegrationCatalogueTests
{
    /// <summary>Every field a catalogue entry needs for configuration is a field its form actually renders.</summary>
    [Fact]
    public void EveryRequiredFieldIsAFormField()
    {
        foreach (var (slug, entry) in IntegrationCatalogue.All)
        {
            var formKeys = entry.Fields.Select(f => f.Key).ToHashSet();

            entry.RequiredFields.Should().BeSubsetOf(formKeys, $"{slug} must only require fields it renders");
        }
    }

    /// <summary>The connection test knows every built-in, and a payment plugin is not in the catalogue.</summary>
    [Fact]
    public void ConnectionTestKnowsEveryBuiltInButNoPlugin()
    {
        foreach (var slug in IntegrationCatalogue.All.Keys)
        {
            IntegrationCatalogue.TryGetTestable(slug, out _).Should().BeTrue();
        }

        IntegrationCatalogue.TryGet("example-gateway", out _).Should().BeFalse();
        IntegrationCatalogue.TryGetTestable("example-gateway", out _).Should().BeFalse();
    }

    /// <summary>An unknown slug is found by neither lookup.</summary>
    [Fact]
    public void UnknownSlugIsNotFound()
    {
        IntegrationCatalogue.TryGet("no-such-integration", out _).Should().BeFalse();
        IntegrationCatalogue.TryGetTestable("no-such-integration", out _).Should().BeFalse();
    }
}
