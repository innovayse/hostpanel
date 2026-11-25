using Innovayse.Hostpanel.Shared.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Shared.Extensions;

/// <summary>
/// Registers <see cref="ExpectedFailureLoggerFactory"/> over the application's logger factory, so an
/// expected validation rejection, or a request the client aborted, stops arriving in the Error log.
/// </summary>
public static class ExpectedFailureLoggingExtensions
{
    /// <summary>
    /// Replaces the registered <see cref="ILoggerFactory"/> with the demoting decorator around it.
    /// Call after logging is configured; the providers registered before stay in effect.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddExpectedFailureLogDemotion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The decorated loggers ask which request an exception belongs to.
        services.AddHttpContextAccessor();

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(ILoggerFactory) && !d.IsKeyedService);
        if (descriptor is null)
        {
            services.AddLogging();
            descriptor = services.Last(d => d.ServiceType == typeof(ILoggerFactory) && !d.IsKeyedService);
        }

        services.Remove(descriptor);
        services.AddSingleton<ILoggerFactory>(
            provider => new ExpectedFailureLoggerFactory(
                ResolveInner(provider, descriptor), provider.GetRequiredService<IHttpContextAccessor>()));

        return services;
    }

    /// <summary>Builds the original factory from its descriptor: an instance, a factory delegate or a type.</summary>
    /// <param name="provider">The root service provider.</param>
    /// <param name="descriptor">The descriptor that was replaced.</param>
    /// <returns>The inner factory.</returns>
    /// <exception cref="InvalidOperationException">The descriptor carries no way to build the factory.</exception>
    private static ILoggerFactory ResolveInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is ILoggerFactory instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (ILoggerFactory)descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            // Usually LoggerFactory itself; built with the container so it receives the registered
            // providers and filter options.
            return (ILoggerFactory)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("The ILoggerFactory registration cannot be resolved.");
    }
}
