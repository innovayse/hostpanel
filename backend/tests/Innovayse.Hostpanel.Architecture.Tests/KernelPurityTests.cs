using System.Reflection;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The kernel and the domain reference nothing but the runtime. EF Core, logging or a localizer
/// reaching the domain is how a business rule ends up depending on a framework. The plugin id rule is a plugin
/// packaging concern and is checked by the use case validators in Application, so the domain does not reference
/// <c>Hostpanel.Sdk</c> or <c>Hostpanel.Sdk.Packaging</c>.
/// </summary>
public sealed class KernelPurityTests
{
    /// <summary>The kernel assembly references only System.* and the runtime facades.</summary>
    [Fact]
    public void SharedKernel_ReferencesOnlyTheRuntime()
    {
        AssertOnlyRuntimeReferences(Layers.SharedKernel);
    }

    /// <summary>The domain assembly references only the runtime and the kernel.</summary>
    [Fact]
    public void Domain_ReferencesOnlyTheRuntimeAndTheKernel()
    {
        AssertOnlyRuntimeReferences(Layers.Domain, Layers.SharedKernel.GetName().Name!);
    }

    /// <summary>
    /// Domain declares no abstract base types of its own in a <c>.Common</c> namespace: every aggregate
    /// derives from the kernel's base entity.
    /// </summary>
    [Fact]
    public void Domain_HasNoBaseTypesOfItsOwn()
    {
        var offenders = Layers.TypesOf(Layers.Domain)
            .Where(t => t.IsClass && t.IsAbstract && t.Namespace is { } ns
                && ns.EndsWith(".Common", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty("base types live in SharedKernel, not in Domain/Common");
    }

    /// <summary>
    /// Every event record implements the kernel's <see cref="IDomainEvent"/> and carries a timestamp.
    /// </summary>
    [Fact]
    public void Domain_EventsCarryOccurredAt()
    {
        var events = Layers.TypesOf(Layers.Domain)
            .Where(t => t.Name.EndsWith("Event", StringComparison.Ordinal) && !t.IsInterface)
            .ToList();

        events.Should().NotBeEmpty();
        events.Should().OnlyContain(
            t => typeof(IDomainEvent).IsAssignableFrom(t), "an event that is not an IDomainEvent is never dispatched");
    }

    /// <summary>Asserts an assembly's references are runtime-only plus the allowed names.</summary>
    /// <param name="assembly">The assembly under test.</param>
    /// <param name="allowed">Assembly names that may be referenced besides the runtime.</param>
    private static void AssertOnlyRuntimeReferences(Assembly assembly, params string[] allowed)
    {
        var offenders = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                           && name != "netstandard"
                           && name != "mscorlib"
                           && !allowed.Contains(name))
            .ToList();

        offenders.Should().BeEmpty(
            "a domain layer that references {0} is no longer pure", string.Join(", ", offenders));
    }
}
