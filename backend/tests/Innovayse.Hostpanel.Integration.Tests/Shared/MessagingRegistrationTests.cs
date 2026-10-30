using Innovayse.Hostpanel.API.Extensions;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Settings;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="MessagingRegistration"/>: how Wolverine gets its handler code in each environment, the
/// execution timeout the chains of the recurring jobs get in place of the one-minute default, and the
/// refusal of service location that every chain is built under.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MessagingRegistrationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A deployed host loads handler code generated at build time and refuses a handler it has none for;
    /// Development compiles on demand when nothing was generated, and the test host always compiles in
    /// memory, so the code of its test doubles is never written into the API's source folder.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="expected">The mode that environment gets.</param>
    [Theory]
    [InlineData("Production", TypeLoadMode.Static)]
    [InlineData("Staging", TypeLoadMode.Static)]
    [InlineData("Demo", TypeLoadMode.Static)]
    [InlineData("Development", TypeLoadMode.Auto)]
    [InlineData("Testing", TypeLoadMode.Dynamic)]
    public void TypeLoadMode_FollowsTheEnvironment(string environment, TypeLoadMode expected)
    {
        MessagingRegistration.TypeLoadModeFor(new HostingEnvironment { EnvironmentName = environment }).Should().Be(expected);
    }

    /// <summary>
    /// Every recurring job command has a chain that may run as long as the job lease, so a pass over
    /// many items is not cut off at Wolverine's default of one minute.
    /// </summary>
    [Fact]
    public void RecurringJobChains_MayRunAsLongAsTheJobLease()
    {
        var lease = factory.Services.GetRequiredService<IOptions<ScheduledJobsOptions>>().Value.Lease;
        var jobs = typeof(ReconcileGatewayPaymentsCronCommand).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRecurringJob).IsAssignableFrom(t))
            .ToList();
        var chains = factory.Services.GetRequiredService<HandlerGraph>().Chains;

        jobs.Should().HaveCountGreaterThanOrEqualTo(13);
        foreach (var job in jobs)
        {
            chains.Single(c => c.MessageType == job).ExecutionTimeoutInSeconds
                .Should().Be((int)lease.TotalSeconds, job.Name);
        }
    }

    /// <summary>A chain that is not a recurring job keeps the default timeout.</summary>
    [Fact]
    public void OtherChains_KeepTheDefaultTimeout()
    {
        var lease = factory.Services.GetRequiredService<IOptions<ScheduledJobsOptions>>().Value.Lease;
        var chains = factory.Services.GetRequiredService<HandlerGraph>().Chains;

        chains.Single(c => c.MessageType == typeof(CompleteGatewayPaymentCommand)).ExecutionTimeoutInSeconds
            .Should().NotBe((int)lease.TotalSeconds);
    }

    /// <summary>
    /// The host refuses to fall back on the container in generated handler code; relaxing it would hide
    /// every future lambda registration, <see cref="IServiceProvider"/> dependency or non-public adapter.
    /// </summary>
    [Fact]
    public void ServiceLocation_IsNotAllowed()
    {
        var runtime = factory.Services.GetRequiredService<IWolverineRuntime>();

        runtime.Options.ServiceLocationPolicy.Should().Be(ServiceLocationPolicy.NotAllowed);
    }

    /// <summary>
    /// Every handler chain of the API builds from constructors alone. Wolverine builds a chain at the
    /// first message of its type, so without this a chain that needs service location would fail only
    /// when that message arrives: a sign-in, or a recurring job that then never runs.
    /// </summary>
    [Fact]
    public void EveryHandlerChain_BuildsWithoutServiceLocation()
    {
        var offenders = ChainsThatFailToBuild(factory);

        offenders.Should().BeEmpty("every chain must be built from constructors; offenders: {0}", string.Join("; ", offenders));
    }

    /// <summary>
    /// The same holds with the public demo switched on, whose container swaps every outward port for an
    /// offline fake, so those fakes are held to the rule as well.
    /// </summary>
    [Fact]
    public void EveryHandlerChain_OfTheDemo_BuildsWithoutServiceLocation()
    {
        var offenders = ChainsThatFailToBuild(DemoTestHost.Enabled(factory));

        offenders.Should().BeEmpty("every chain must be built from constructors; offenders: {0}", string.Join("; ", offenders));
    }

    /// <summary>
    /// The production container, with no adapter swapped for a test double, builds every chain from
    /// constructors too. The shared host replaces the network-bound ports (SSH agents, registrars,
    /// provisioning, mail, probes) with public fakes, so a production adapter that is not public, or that
    /// takes a dependency that is not, would pass the two tests above and fail only on a deployment.
    /// </summary>
    /// <param name="demo">Whether the public demo is switched on.</param>
    /// <returns>A task that completes when every chain was built.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryHandlerChain_OfTheProductionContainer_BuildsWithoutServiceLocation(bool demo)
    {
        await using var production = IntegrationTestFactory.WithProductionAdapters();
        await production.InitializeAsync();

        var offenders = ChainsThatFailToBuild(demo ? DemoTestHost.Enabled(production) : production);

        offenders.Should().BeEmpty("every chain must be built from constructors; offenders: {0}", string.Join("; ", offenders));
    }

    /// <summary>
    /// Every type the production composition root registers (Infrastructure, API, Application, the plugin
    /// loader and the demo) is one Wolverine can construct: public, with a public constructor whose
    /// parameters are all public, and no scoped or transient service of ours is built by a factory
    /// lambda. The only exceptions are the services <see cref="MessagingRegistration.ContainerBuiltServices"/>
    /// names. This holds for registrations no handler reaches today, so the next handler that takes one
    /// does not fail on its first message.
    /// </summary>
    /// <param name="demo">Whether the public demo is switched on.</param>
    /// <returns>A task that completes when every registration was checked.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryProductionRegistration_IsConstructibleWithoutServiceLocation(bool demo)
    {
        await using var production = IntegrationTestFactory.WithProductionAdapters();
        await production.InitializeAsync();
        var host = demo ? DemoTestHost.Enabled(production) : production;

        var offenders = RegistrationsThatNeedServiceLocation(host.Services.GetRequiredService<ServiceRegistrationSnapshot>());

        offenders.Should().BeEmpty("every registration must be constructible by Wolverine; offenders: {0}", string.Join("; ", offenders));
    }

    /// <summary>
    /// Names every registration of ours Wolverine could not construct: an implementation type that is not
    /// public or has no public constructor, a public constructor parameter whose type is not public, or a
    /// scoped or transient service built by a factory. Services named by
    /// <see cref="MessagingRegistration.ContainerBuiltServices"/> are exempt.
    /// </summary>
    /// <param name="snapshot">The registrations the composition root made.</param>
    /// <returns>One line per offending registration.</returns>
    private static List<string> RegistrationsThatNeedServiceLocation(ServiceRegistrationSnapshot snapshot)
    {
        var exempt = MessagingRegistration.ContainerBuiltServices(snapshot.Descriptors).ToHashSet();
        var offenders = new List<string>();

        snapshot.Descriptors.Should().NotBeEmpty("the composition root registers the production services");
        foreach (var descriptor in snapshot.Descriptors)
        {
            if (exempt.Contains(descriptor.ServiceType))
            {
                continue;
            }

            var implementation = descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;
            if (implementation is null)
            {
                // A keyed descriptor throws on ImplementationFactory; keyed services are the plugins,
                // which the exempt activator builds, so only unkeyed factories are judged.
                if (!descriptor.IsKeyedService
                    && descriptor.ImplementationFactory is not null
                    && descriptor.Lifetime != ServiceLifetime.Singleton
                    && IsOurs(descriptor.ServiceType))
                {
                    offenders.Add($"{NameOf(descriptor.ServiceType)}: {descriptor.Lifetime} factory registration");
                }

                continue;
            }

            if (!IsOurs(implementation))
            {
                continue;
            }

            if (!implementation.IsVisible)
            {
                offenders.Add($"{NameOf(descriptor.ServiceType)} -> {NameOf(implementation)}: not public");
            }

            var constructors = implementation.GetConstructors();
            if (constructors.Length == 0)
            {
                offenders.Add($"{NameOf(implementation)}: no public constructor");
            }

            foreach (var parameter in constructors.SelectMany(constructor => constructor.GetParameters()))
            {
                if (!parameter.ParameterType.IsVisible)
                {
                    offenders.Add($"{NameOf(implementation)}({parameter.Name}): {NameOf(parameter.ParameterType)} is not public");
                }
            }
        }

        return offenders.Distinct().ToList();
    }

    /// <summary>Whether a type is one of this solution's own, as opposed to the framework's or a vendor's.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for the host, provider and SDK assemblies.</returns>
    private static bool IsOurs(Type type)
    {
        var assembly = type.Assembly.GetName().Name ?? string.Empty;

        return assembly.StartsWith("Innovayse.Hostpanel.", StringComparison.Ordinal)
            || assembly.StartsWith("Innovayse.Providers.", StringComparison.Ordinal)
            || assembly == "Innovayse.SDK";
    }

    /// <summary>A readable name for a type in an offender line.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its namespace-qualified name.</returns>
    private static string NameOf(Type type) => $"{type.Namespace}.{type.Name}";

    /// <summary>Builds the handler of every chain the host discovered and names the ones that fail.</summary>
    /// <param name="host">The host whose chains are built.</param>
    /// <returns>One line per chain that could not be built: its message type and the first line of the reason.</returns>
    internal static List<string> ChainsThatFailToBuild(WebApplicationFactory<Program> host)
    {
        var graph = host.Services.GetRequiredService<HandlerGraph>();
        var offenders = new List<string>();

        graph.Chains.Should().NotBeEmpty("the handlers of the API and Application assemblies are discovered");
        foreach (var chain in graph.Chains)
        {
            try
            {
                graph.HandlerFor(chain.MessageType);
            }
            catch (Exception exception)
            {
                var reason = exception.Message
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ?? exception.GetType().Name;
                offenders.Add($"{chain.MessageType.Name}: {exception.GetType().Name}: {reason}");
            }
        }

        return offenders;
    }
}
