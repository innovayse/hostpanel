using System.Reflection;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>One handle per layer assembly, so a rule names the layer and not a type it happens to contain.</summary>
internal static class Layers
{
    /// <summary>The kernel: base entity, value object, domain events.</summary>
    public static readonly Assembly SharedKernel = typeof(Innovayse.Hostpanel.SharedKernel.Domain.BaseEntity).Assembly;

    /// <summary>The domain: aggregates and their rules.</summary>
    public static readonly Assembly Domain = typeof(Innovayse.Hostpanel.Domain.Billing.Invoice).Assembly;

    /// <summary>The cross-module edge shapes: Result, the base controller, middleware.</summary>
    public static readonly Assembly Shared = typeof(Innovayse.Hostpanel.Shared.Models.Result).Assembly;

    /// <summary>The application layer: use cases, ports, resources.</summary>
    public static readonly Assembly Application = typeof(Innovayse.Hostpanel.Application.Common.Errors.HostpanelErrorCodes).Assembly;

    /// <summary>The infrastructure layer: the DbContext, adapters and external clients.</summary>
    public static readonly Assembly Infrastructure = typeof(Innovayse.Hostpanel.Infrastructure.Persistence.AppDbContext).Assembly;

    /// <summary>The API layer: controllers and the HTTP edge.</summary>
    public static readonly Assembly Api = typeof(Innovayse.Hostpanel.API.Controllers.HealthController).Assembly;

    /// <summary>The plugin SDK: contracts every provider implements.</summary>
    public static readonly Assembly Sdk = typeof(Innovayse.SDK.Plugins.Manifest.PluginManifest).Assembly;

    /// <summary>The CWP provider plugin.</summary>
    public static readonly Assembly ProviderCwp = typeof(Innovayse.Providers.CWP.CwpProvisioningProvider).Assembly;

    /// <summary>The CWP7 provider plugin.</summary>
    public static readonly Assembly ProviderCwp7 = typeof(Innovayse.Providers.CWP7.Cwp7ProvisioningProvider).Assembly;

    /// <summary>The Inecobank payment provider plugin.</summary>
    public static readonly Assembly ProviderInecobank = typeof(Innovayse.Providers.Inecobank.InecobankPaymentGateway).Assembly;

    /// <summary>
    /// The types a developer wrote in an assembly: loadable types minus compiler-generated closures,
    /// state machines and display classes, which no layout rule is about.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>Its hand-written types.</returns>
    public static IReadOnlyList<Type> AuthoredTypesOf(Assembly assembly)
    {
        return TypesOf(assembly)
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal)
                && !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .ToList();
    }

    /// <summary>Loads every type an assembly can expose, tolerating a partial load.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>Its loadable types.</returns>
    public static IReadOnlyList<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null).Select(type => type!).ToList();
        }
    }
}
