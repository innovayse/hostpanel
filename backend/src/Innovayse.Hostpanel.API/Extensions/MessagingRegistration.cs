using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Middleware;
using Innovayse.Hostpanel.Shared.Settings;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Extensions;

/// <summary>
/// Wires Wolverine as the process's only bus: handler discovery over the API and Application
/// assemblies, the validation and logging policies every chain runs through, and the two
/// middleware types those policies insert.
/// </summary>
public static class MessagingRegistration
{
    /// <summary>
    /// Chooses how Wolverine gets the code of its message handlers. Compiling a handler at runtime costs
    /// tens of megabytes that stay resident, for every handler on its first use, so a deployed host loads
    /// code generated at image build time (<c>codegen write</c>, see the API Dockerfile) and refuses to
    /// start a handler it has no generated code for. Development has no generated code to load and
    /// compiles on demand, using generated code only when it is there. The test host always compiles in
    /// memory and writes nothing: a test host swaps services for test doubles, so its handler code names
    /// test types, and written into the API's source folder it would break the next build of the API
    /// or be loaded, stale, by a later host.
    /// </summary>
    /// <param name="environment">The environment the host runs in.</param>
    /// <returns>
    /// <see cref="TypeLoadMode.Auto"/> in Development, <see cref="TypeLoadMode.Dynamic"/> in Testing,
    /// <see cref="TypeLoadMode.Static"/> everywhere else.
    /// </returns>
    public static TypeLoadMode TypeLoadModeFor(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsEnvironment(EnvironmentNames.Testing))
        {
            return TypeLoadMode.Dynamic;
        }

        return environment.IsDevelopment() ? TypeLoadMode.Auto : TypeLoadMode.Static;
    }

    /// <summary>Adds Wolverine to the host.</summary>
    /// <param name="builder">The host builder, needed for both its container and <c>UseWolverine</c>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static WebApplicationBuilder AddHostpanelWolverine(this WebApplicationBuilder builder)
    {
        // The two policies below insert these into every chain, and Wolverine's generated code
        // resolves them from the container, so they are registered here or every dispatch fails.
        builder.Services.AddTransient(typeof(ValidationMiddleware<>));
        builder.Services.AddTransient<LoggingMiddleware>();
        builder.Services.AddTransient<DemoGuardMiddleware>();

        // Read here, not through IOptions: the policies are built while Wolverine is configured, before
        // the container exists. The same section is bound and validated at startup in AddApi.
        var jobs = builder.Configuration.GetSection(ScheduledJobsOptions.SectionName).Get<ScheduledJobsOptions>()
            ?? new ScheduledJobsOptions();
        var typeLoadMode = TypeLoadModeFor(builder.Environment);

        builder.Host.UseWolverine(opts =>
        {
            opts.CodeGeneration.TypeLoadMode = typeLoadMode;

            // Wolverine builds a handler's dependencies as plain constructor calls in its generated
            // code. Falling back on the container instead is refused, in every environment: a lambda
            // registration, an IServiceProvider in a constructor or a type that is not public would
            // otherwise send that handler through the container quietly, and Wolverine 6 refuses it
            // anyway. The few services only the container can build are named once, by the call below,
            // and MessagingRegistrationTests builds every chain so a new offender fails a test instead
            // of the first message of its type.
            opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;
            UseServiceLocationForContainerBuiltServices(opts, builder.Services);

            opts.Discovery.IncludeAssembly(typeof(MessagingRegistration).Assembly);
            opts.Discovery.IncludeAssembly(typeof(Innovayse.Hostpanel.Application.Clients.Commands.AcceptInvitation.AcceptInvitationCommand).Assembly);

            // A recurring job's pass loops over every item the job owns and can outrun Wolverine's
            // one-minute handler timeout, which would cut it short and leave the rest for the next day.
            // Its chain gets the job lease instead: a pass that outlasts the lease is started a second
            // time by another instance anyway, so that is the longest a pass may run.
            opts.Policies.Add(new RecurringJobTimeoutPolicy(jobs.Lease));

            // Validation at position 0 of every discovered chain, so no handler has to
            // remember to ask and no rule can be written in a validator and quietly never run. A
            // message with no validator passes straight through. A policy rather than an opt-in marker:
            // a handler whose author forgot the marker would look identical to one whose rules passed.
            //
            // On failure the middleware throws FluentValidation's ValidationException, answered as a
            // 400 VALIDATION_ERROR with the field errors grouped by property. Not a 500: the request
            // was wrong, not the server.
            opts.Policies.Add<ValidationPolicy>();

            // "Handling {RequestName} for User {UserId}" before and "Handled {RequestName} successfully" after,
            // on every chain.
            opts.Policies.Add<LoggingPolicy>();

            // Added last on purpose: it inserts at position 0, so the guard added last
            // runs first and a restricted command is refused before it is validated or logged.
            opts.Policies.Add<DemoGuardPolicy>();
        });

        return builder;
    }

    /// <summary>
    /// The documented exemption list: the service types only the container can build, which Wolverine's
    /// generated code takes from the message's container scope. Every other registration must be a public
    /// type with a public constructor whose parameters are public, and the integration tests walk the
    /// production registrations against this one list.
    /// </summary>
    /// <param name="services">The registrations, a live service collection or a snapshot of one, read for the typed HTTP clients registered in it.</param>
    /// <returns>The exempt service types, without duplicates.</returns>
    /// <remarks>
    /// Each kind below is built by the framework, not by a constructor Wolverine could call:
    /// <list type="bullet">
    /// <item><description>
    /// <b>The data context.</b> <c>AddDbContext</c> builds <see cref="AppDbContext"/> from its options
    /// lambda (the interceptors are added there), and <see cref="IHostpanelDbContext"/> forwards to that
    /// same scoped instance, so a handler and everything it saves through share one unit of work.
    /// </description></item>
    /// <item><description>
    /// <b>Identity's <see cref="UserManager{TUser}"/>.</b> Its constructor takes the
    /// <see cref="IServiceProvider"/> itself (it resolves the token providers by name at run time), so
    /// no registration of ours can make it constructible; every local-mode account adapter depends on it.
    /// </description></item>
    /// <item><description>
    /// <b>The plugin activator.</b> It builds a plugin type loaded from a folder at run time, whose
    /// constructor is only known then, so it hands the container to <c>ActivatorUtilities</c>. Every plugin
    /// resolver (payment, fulfilment, identity) goes through it, so the resolvers themselves are plain
    /// constructor-built adapters and only this one type is located.
    /// </description></item>
    /// <item><description>
    /// <b>Typed HTTP clients.</b> <c>AddHttpClient&lt;T&gt;</c> registers a transient factory that asks
    /// <c>IHttpClientFactory</c> for the client, so the handler pipeline (resilience, outbound address
    /// guard, timeouts) of that registration applies. Found by the framework's factory rather than listed,
    /// so a typed client added later needs nothing here; any other lambda registration is still refused.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<Type> ContainerBuiltServices(IEnumerable<ServiceDescriptor> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The assembly AddHttpClient<T> lives in; the factory lambda it registers is compiled into it.
        var httpClientFactoryAssembly = typeof(HttpClientFactoryServiceCollectionExtensions).Assembly;

        // A keyed descriptor throws on ImplementationFactory, so it is ruled out first; plugins are keyed.
        var typedClients = services
            .Where(descriptor => !descriptor.IsKeyedService
                && descriptor.Lifetime == ServiceLifetime.Transient
                && descriptor.ImplementationFactory?.Method.Module.Assembly == httpClientFactoryAssembly)
            .Select(descriptor => descriptor.ServiceType);

        Type[] containerBuilt =
        [
            typeof(AppDbContext),
            typeof(IHostpanelDbContext),
            typeof(UserManager<AppUser>),
            typeof(PluginActivator),
        ];

        return containerBuilt.Concat(typedClients).Distinct().ToList();
    }

    /// <summary>
    /// Names the services Wolverine's generated code takes from the message's container scope instead
    /// of constructing them, so every other dependency is held to <see cref="ServiceLocationPolicy.NotAllowed"/>.
    /// </summary>
    /// <param name="opts">The Wolverine options being configured.</param>
    /// <param name="services">The service collection, read for the typed HTTP clients registered in it.</param>
    private static void UseServiceLocationForContainerBuiltServices(WolverineOptions opts, IServiceCollection services)
    {
        foreach (var serviceType in ContainerBuiltServices(services))
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor(serviceType);
        }
    }
}
