namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>A closed value set is an enum, and every enum sits in an <c>Enums</c> folder and namespace.</summary>
public sealed class EnumPlacementTests
{
    /// <summary>Every top-level enum in the product's own assemblies, the SDK and the providers has <c>.Enums</c> in its namespace.</summary>
    [Fact]
    public void Enums_LiveOnlyInEnumsNamespaces()
    {
        var offenders = new[]
            {
                Layers.SharedKernel, Layers.Domain, Layers.Application, Layers.Shared, Layers.Infrastructure, Layers.Api,
                Layers.Sdk, Layers.ProviderCwp, Layers.ProviderCwp7,
            }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => type.IsEnum && !type.IsNested)
            .Where(type => type.Namespace is null || !type.Namespace.EndsWith(".Enums", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("move each enum into an Enums folder beside its owner");
    }
}
