using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Architecture.Tests.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// A module owns its tables (rules/architecture.md): an Application class in one module reads or
/// writes another module's <see cref="DbSet{TEntity}"/> only through that module's command, query
/// or a port. Each set on <see cref="IHostpanelDbContext"/> belongs to the module named by its
/// entity's Domain namespace segment; the test reads the IL of every top-level Application class,
/// handlers as well as resolvers, seeders and extension classes on the context, state machines and
/// lambdas included, for the set getters it calls. <see cref="CrossModuleReadModels"/> lists the read models
/// that may span modules and the documented writers that may write them.
/// </summary>
public sealed class ModuleTableOwnershipTests
{
    /// <summary>The namespace prefix every Application module sits under.</summary>
    private const string ApplicationPrefix = "Innovayse.Hostpanel.Application.";

    /// <summary>The namespace prefix every Domain area sits under.</summary>
    private const string DomainPrefix = "Innovayse.Hostpanel.Domain.";

    /// <summary>
    /// Domain areas (and Application folders) that are not a module of their own, mapped to the module whose
    /// use cases own their tables. An Application folder named here counts as the module it maps to.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DomainAreaOwners = new Dictionary<string, string>
    {
        // Settings are written only by the admin settings and integration use cases.
        ["Settings"] = "Admin",

        // Servers, server groups and per-server SSH settings are managed under Application/Admin/Servers.
        ["Servers"] = "Admin",

        // Client SSH keys are added, listed and revoked by the Services use cases.
        ["Ssh"] = "Services",

        // SSL checks are written by the Reports monitoring command.
        ["Ssl"] = "Reports",

        // Disk usage samples are written by the Reports disk usage command.
        ["Hosting"] = "Reports",

        // Provisioning is the hosting side of the Services module: it drives the same ClientServices rows through their lifecycle.
        ["Provisioning"] = "Services",

        // Plugin management (Application/Admin/Plugins) and the plugin runtime (Application/Plugins) are one feature split by audience:
        // the operator lists, removes and purges the plugin store rows (PluginDataScopes) that the runtime writes, so the tables belong to Admin.
        ["Plugins"] = "Admin",
    };

    /// <summary>No Application class touches a set owned by another module unless it is an allow-listed read model.</summary>
    [Fact]
    public void ApplicationClasses_TouchOnlyTheirOwnModulesTables()
    {
        var owners = SetOwners();

        var offenders = ApplicationClasses()
            .Where(type => !CrossModuleReadModels.All.Any(entry => entry.Covers(type)))
            .SelectMany(type => SetsTouchedBy(type)
                .Where(set => owners.ContainsKey(set) && owners[set] != ModuleOf(type))
                .Select(set => $"{type.FullName} -> {set} ({owners[set]})"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a module reaches another module's tables only through that module's command, query or a port");
    }

    /// <summary>Every set resolves to a module that exists in the Application layer, so no set escapes the check.</summary>
    [Fact]
    public void EverySet_HasAnApplicationModuleOwner()
    {
        var modules = Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith(ApplicationPrefix, StringComparison.Ordinal))
            .Select(type => ModuleOf(type))
            .ToHashSet(StringComparer.Ordinal);

        var orphans = SetOwners()
            .Where(pair => !modules.Contains(pair.Value))
            .Select(pair => $"{pair.Key} ({pair.Value})")
            .ToList();

        orphans.Should().BeEmpty("each Domain area maps to the Application module that owns its tables");
    }

    /// <summary>Every allow-list entry still covers a class that crosses modules, so stale exceptions are removed.</summary>
    [Fact]
    public void EveryAllowListEntry_IsStillNeeded()
    {
        var owners = SetOwners();
        var crossing = ApplicationClasses()
            .Where(type => SetsTouchedBy(type)
                .Any(set => owners.ContainsKey(set) && owners[set] != ModuleOf(type)))
            .ToList();

        var stale = CrossModuleReadModels.All
            .Where(entry => !crossing.Any(type => entry.Covers(type)))
            .Select(entry => entry.Scope)
            .ToList();

        stale.Should().BeEmpty("an exception that no class needs any more is removed from the allow-list");
    }

    /// <summary>A writer exception is the rare case, so each one names its scope and carries its written reason.</summary>
    [Fact]
    public void EveryWriterException_CarriesAWrittenReason()
    {
        var writers = CrossModuleReadModels.All.Where(entry => entry.Access == CrossModuleAccess.Writer).ToList();

        writers.Should().OnlyContain(
            entry => entry.Scope.StartsWith(ApplicationPrefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(entry.Reason),
            "a writer that spans modules is allowed only as a documented exception with its reason");
    }

    /// <summary>Static classes, such as extension classes on the context, are inspected alongside handlers.</summary>
    [Fact]
    public void Inspection_IncludesStaticClasses()
    {
        var extensions = typeof(Innovayse.Hostpanel.Application.Admin.Servers.Extensions.ServerSshSettingsExtensions);

        ApplicationClasses().Should().Contain(extensions);
    }

    /// <summary>The detector sees a set read inside an async handler's state machine.</summary>
    [Fact]
    public void Detector_SeesSetsReadInsideAsyncHandlers()
    {
        var handler = typeof(Innovayse.Hostpanel.Application.Clients.Queries.GetClientSummary.GetClientSummaryHandler);

        SetsTouchedBy(handler).Should().Contain("Invoices").And.Contain("Clients");
    }

    /// <summary>Maps each set on the context to the module owning its entity.</summary>
    /// <returns>Set property name to owning Application module.</returns>
    private static Dictionary<string, string> SetOwners()
    {
        return typeof(IHostpanelDbContext)
            .GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToDictionary(
                property => property.Name,
                property => OwnerOf(property.PropertyType.GetGenericArguments()[0]),
                StringComparer.Ordinal);
    }

    /// <summary>The owning module of an entity: its Domain area, translated where the names differ.</summary>
    /// <param name="entity">The entity type.</param>
    /// <returns>The Application module name.</returns>
    private static string OwnerOf(Type entity)
    {
        var area = SegmentAfter(entity.Namespace ?? string.Empty, DomainPrefix);

        return DomainAreaOwners.TryGetValue(area, out var owner) ? owner : area;
    }

    /// <summary>The Application module a type belongs to.</summary>
    /// <param name="type">The Application type.</param>
    /// <returns>The first namespace segment after the Application prefix, translated where that folder is part of another module.</returns>
    private static string ModuleOf(Type type)
    {
        var module = SegmentAfter(type.Namespace ?? string.Empty, ApplicationPrefix);

        // An Application folder that is part of another module, such as Provisioning, counts as that module.
        return DomainAreaOwners.TryGetValue(module, out var owner) ? owner : module;
    }

    /// <summary>The namespace segment right after a prefix.</summary>
    /// <param name="ns">The namespace.</param>
    /// <param name="prefix">The prefix, ending in a dot.</param>
    /// <returns>The segment, or an empty string when the namespace is outside the prefix.</returns>
    private static string SegmentAfter(string ns, string prefix)
    {
        if (!ns.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return ns[prefix.Length..].Split('.')[0];
    }

    /// <summary>
    /// Every top-level concrete or static class in the Application layer: use case and event handlers,
    /// resolvers, seeders, importers and extension classes. Nested compiler-generated types are read
    /// through their outer class.
    /// </summary>
    /// <returns>The class types.</returns>
    private static IEnumerable<Type> ApplicationClasses()
    {
        return Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.IsClass && !type.IsNested
                && (!type.IsAbstract || type.IsSealed)
                && type.Namespace is not null
                && type.Namespace.StartsWith(ApplicationPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The context sets a class calls the getter of, across its own methods and every nested
    /// compiler-generated type (async state machines, lambda closures).
    /// </summary>
    /// <param name="type">The class.</param>
    /// <returns>The set property names.</returns>
    private static HashSet<string> SetsTouchedBy(Type type)
    {
        return MethodBodyReader.CallsMadeBy(type)
            .Where(called => called.DeclaringType == typeof(IHostpanelDbContext)
                && called.IsSpecialName
                && called.Name.StartsWith("get_", StringComparison.Ordinal))
            .Select(called => called.Name["get_".Length..])
            .ToHashSet(StringComparer.Ordinal);
    }
}
