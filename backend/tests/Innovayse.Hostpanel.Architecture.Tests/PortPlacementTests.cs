namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// An interface the Application layer owns is a port, and ports live in one folder so a reader finds
/// every seam to the outside in one listing (rules/file-layout.md).
/// </summary>
public sealed class PortPlacementTests
{
    /// <summary>The only namespace in which the Application layer may declare an interface.</summary>
    private const string PortNamespace = "Innovayse.Hostpanel.Application.Common.Interfaces";

    /// <summary>Every top-level interface in the Application assembly sits in <c>Common/Interfaces</c>.</summary>
    [Fact]
    public void ApplicationInterfaces_LiveOnlyInCommonInterfaces()
    {
        var offenders = Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.IsInterface && !type.IsNested && type.Namespace != PortNamespace)
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("ports belong in Application/Common/Interfaces");
    }

    /// <summary>
    /// Request-scoped adapters: the only port implementations allowed under the API, each because
    /// it reads the current request. The value names what it reads.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, string> RequestScopedAdapters = new Dictionary<Type, string>
    {
        [typeof(Innovayse.Hostpanel.API.Identity.HttpCallerAccessToken)] = "the request's Authorization header, through IHttpContextAccessor",
        [typeof(Innovayse.Hostpanel.API.Identity.HttpCallerClientMemo)] = "the request's HttpContext.Items, through IHttpContextAccessor",
    };

    /// <summary>
    /// Contracts that sit in <c>Common/Interfaces</c> but are not ports: the Application layer implements
    /// them itself by design, so no adapter exists. The value names where the exception is written down.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, string> ApplicationContracts = new Dictionary<Type, string>
    {
        [typeof(Innovayse.Hostpanel.Application.Common.Interfaces.IMigrationEntityImporter)] =
            "the migration importer registry; each importer is migration use-case code (rules/project.md, migration imports)",
    };

    /// <summary>
    /// Every class implementing a port is an adapter in Infrastructure, or a request-scoped adapter
    /// under the API listed in <see cref="RequestScopedAdapters"/>. The CWP7 provider project is not
    /// scanned: Infrastructure links it directly and builds it per server, so it is an
    /// Infrastructure adapter in its own assembly (rules/project.md).
    /// </summary>
    [Fact]
    public void PortImplementations_LiveInInfrastructure()
    {
        var ports = Ports();

        var offenders = new[]
            {
                Layers.Application, Layers.Shared, Layers.Api,
                Layers.Sdk, Layers.ProviderCwp, Layers.ProviderInecobank,
            }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => type.IsClass && !type.IsAbstract && !RequestScopedAdapters.ContainsKey(type))
            .SelectMany(type => type.GetInterfaces()
                .Where(ports.Contains)
                .Select(port => $"{type.FullName} : {port.Name}"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty("a port is implemented by an Infrastructure adapter, never beside the use cases");
    }

    /// <summary>The scan finds the ports; a rule over zero ports would pass for the wrong reason.</summary>
    [Fact]
    public void TheScan_FindsThePorts()
    {
        Ports().Should().HaveCountGreaterThan(20);
    }

    /// <summary>
    /// The ports: interfaces in <c>Common/Interfaces</c> that declare at least one method. An
    /// interface of properties only, such as a marker a command or query carries, is a message
    /// contract and has no adapter; the <see cref="ApplicationContracts"/> are implemented in Application.
    /// </summary>
    /// <returns>The port interfaces.</returns>
    internal static IReadOnlySet<Type> Ports()
    {
        return Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.IsInterface && type.Namespace == PortNamespace
                && !ApplicationContracts.ContainsKey(type)
                && type.GetMethods().Any(method => !method.IsSpecialName))
            .ToHashSet();
    }
}
