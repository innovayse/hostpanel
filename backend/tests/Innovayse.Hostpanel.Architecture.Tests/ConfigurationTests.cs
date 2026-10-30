using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Configuration is typed, sealed and bound once at the composition root (rules/configuration.md):
/// nothing below it holds <see cref="IConfiguration"/>, and every options class is sealed and names
/// where it is bound from.
/// </summary>
public sealed class ConfigurationTests
{
    /// <summary>The name every composition-root class carries, one per layer.</summary>
    private const string CompositionRootName = "DependencyInjection";

    /// <summary>
    /// The API namespace whose static <c>*Registration</c> classes are the composition root split by
    /// concern (hostpanel/rules/project.md: <c>Extensions/MessagingRegistration.cs</c>,
    /// <c>Extensions/AuthenticationRegistration.cs</c>), called from <c>Program.cs</c> only.
    /// </summary>
    private const string ApiRegistrationNamespace = "Innovayse.Hostpanel.API.Extensions";

    /// <summary>The constant naming the configuration section an options class binds.</summary>
    private const string SectionNameConstant = "SectionName";

    /// <summary>The constant naming the single top-level key an options class binds when it is not a section.</summary>
    private const string ConfigurationKeyConstant = "ConfigurationKey";

    /// <summary>The suffix every options class carries.</summary>
    private const string OptionsSuffix = "Options";

    /// <summary>
    /// Types below the composition root that may hold <see cref="IConfiguration"/>, each a documented
    /// exception in hostpanel/rules/project.md. The plugin activator hands every plugin the
    /// <see cref="IConfiguration"/> the SDK's base classes take in their public constructor; typed plugin
    /// settings are planned for a later SDK version.
    /// </summary>
    private static readonly HashSet<string> ConfigurationExceptions = new(StringComparer.Ordinal)
    {
        "Innovayse.Hostpanel.Infrastructure.Plugins.PluginActivator",
    };

    /// <summary>
    /// No type below the composition root takes, holds or returns <see cref="IConfiguration"/>: only
    /// <c>Program</c>, the <c>DependencyInjection</c> classes and the API's registration classes read
    /// configuration by key.
    /// </summary>
    [Fact]
    public void IConfiguration_StaysInTheCompositionRoot()
    {
        var offenders = ProductLayers()
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => !IsCompositionRoot(type))
            .Where(type => !ConfigurationExceptions.Contains(type.FullName!))
            .Where(type => TypeSignatures.TypesNamedBy(type).Any(typeof(IConfiguration).IsAssignableFrom))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty("a handler, adapter or helper takes typed options, never IConfiguration");
    }

    /// <summary>
    /// The exception list is not stale: each entry still exists and still holds
    /// <see cref="IConfiguration"/>, so a removed or fixed type cannot leave an entry behind.
    /// </summary>
    [Fact]
    public void ConfigurationExceptions_ContainOnlyTypesThatStillNeedThem()
    {
        var stillNeeded = ProductLayers()
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => TypeSignatures.TypesNamedBy(type).Any(typeof(IConfiguration).IsAssignableFrom))
            .Select(type => type.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        ConfigurationExceptions.Except(stillNeeded).Should().BeEmpty("delete an exception when its type goes or stops holding IConfiguration");
    }

    /// <summary>Every options class is sealed.</summary>
    [Fact]
    public void Options_AreSealed()
    {
        var offenders = OptionsClasses()
            .Where(type => !type.IsSealed)
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("an options class is a sealed bag of settings");
    }

    /// <summary>
    /// Every options class names where it is bound from: a <c>SectionName</c> constant, a
    /// <c>ConfigurationKey</c> constant for a single top-level key, or it is a nested section that is
    /// the property type of another options class.
    /// </summary>
    [Fact]
    public void Options_NameTheirSection()
    {
        var options = OptionsClasses();
        var nestedSections = options
            .SelectMany(type => type.GetProperties().Select(property => property.PropertyType))
            .ToHashSet();

        var offenders = options
            .Where(type => !nestedSections.Contains(type))
            .Where(type => !HasStringConstant(type, SectionNameConstant) && !HasStringConstant(type, ConfigurationKeyConstant))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("the registration references the section name on the class, so a rename is a compile error");
    }

    /// <summary>The scan finds the options classes; a rule over none would pass for the wrong reason.</summary>
    [Fact]
    public void TheScan_FindsTheOptions()
    {
        OptionsClasses().Should().HaveCountGreaterThan(20);
    }

    /// <summary>The product's own layer assemblies.</summary>
    /// <returns>Kernel, Domain, Shared, Application, Infrastructure and API.</returns>
    private static Assembly[] ProductLayers()
    {
        return [Layers.SharedKernel, Layers.Domain, Layers.Shared, Layers.Application, Layers.Infrastructure, Layers.Api];
    }

    /// <summary>Every top-level class named <c>*Options</c> in the product's layers.</summary>
    /// <returns>The options classes.</returns>
    private static IReadOnlyList<Type> OptionsClasses()
    {
        return ProductLayers()
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => type.IsClass && !type.IsNested && type.Name.EndsWith(OptionsSuffix, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Whether a type is part of the composition root.</summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> for <c>Program</c>, a <c>DependencyInjection</c> class or an API registration class.</returns>
    private static bool IsCompositionRoot(Type type)
    {
        var outer = type;
        while (outer.DeclaringType is not null)
        {
            outer = outer.DeclaringType;
        }

        return outer == typeof(Program)
            || (outer.IsAbstract && outer.IsSealed && outer.Name == CompositionRootName)
            || (outer.IsAbstract && outer.IsSealed && outer.Namespace == ApiRegistrationNamespace
                && outer.Name.EndsWith("Registration", StringComparison.Ordinal));
    }

    /// <summary>Whether a type declares a public string constant of the given name.</summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The constant's name.</param>
    /// <returns><c>true</c> when the constant exists.</returns>
    private static bool HasStringConstant(Type type, string name)
    {
        return type.GetField(name, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } field
            && field.FieldType == typeof(string);
    }
}
