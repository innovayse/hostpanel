using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Middleware;
using Innovayse.Hostpanel.Shared.Settings;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Identity;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace Innovayse.Hostpanel.API.Extensions;

/// <summary>
/// Wires Wolverine as the process's only bus: handler discovery over the API and Application
/// assemblies, durable message storage in PostgreSQL, the validation, logging and retry policies
/// every chain runs through, and the middleware types those policies insert.
/// </summary>
public static class MessagingRegistration
{
    /// <summary>
    /// The connection string name Wolverine's message storage is read from: the application's own
    /// database, so an outbox row and the state change it announces share one transaction.
    /// </summary>
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// The PostgreSQL schema Wolverine keeps its envelope, dead-letter and node tables in. Kept apart
    /// from <c>public</c> so the framework's lower-case tables never mix with the PascalCase EF model.
    /// </summary>
    public const string MessageStorageSchema = "wolverine";

    /// <summary>
    /// The one in-memory local queue every recurring job (<see cref="IRecurringJob"/>) is routed to.
    /// </summary>
    public const string RecurringJobsQueue = "recurring-jobs";

    /// <summary>
    /// Chooses how Wolverine gets the code of its message handlers. Compiling a handler at runtime costs
    /// tens of megabytes that stay resident, for every handler on its first use, so a deployed host loads
    /// code generated at image build time (<c>codegen write</c>, see the API Dockerfile) and refuses to
    /// start a handler it has no generated code for. Development and the test host compile in memory and
    /// write nothing. Development must not use <see cref="TypeLoadMode.Auto"/>: Auto writes the generated
    /// files into <c>Internal/Generated</c> inside the source tree while the API runs, and
    /// <c>dotnet watch</c> polling that tree sees every new file, then crashes on its own directory
    /// enumeration (<c>An item with the same key has already been added</c>) and takes the API with it.
    /// A test host swaps services for test doubles, so its handler code names test types, and written into
    /// the API's source folder it would break the next build of the API or be loaded, stale, by a later host.
    /// </summary>
    /// <param name="environment">The environment the host runs in.</param>
    /// <returns>
    /// <see cref="TypeLoadMode.Dynamic"/> in Development and in Testing,
    /// <see cref="TypeLoadMode.Static"/> everywhere else.
    /// </returns>
    public static TypeLoadMode TypeLoadModeFor(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsEnvironment(EnvironmentNames.Testing) || environment.IsDevelopment())
        {
            return TypeLoadMode.Dynamic;
        }

        return TypeLoadMode.Static;
    }

    /// <summary>
    /// Whether the host asserts, at startup, that every pre-generated type it will load exists. Only a mode
    /// that loads generated code has anything to assert: a mode that compiles on demand writes what is
    /// missing, and asserting there would fail a development or test host that has no generated code yet.
    /// </summary>
    /// <param name="typeLoadMode">The mode <see cref="TypeLoadModeFor"/> chose.</param>
    /// <returns><see langword="true"/> for <see cref="TypeLoadMode.Static"/>.</returns>
    public static bool AssertsPreGeneratedTypes(TypeLoadMode typeLoadMode) => typeLoadMode == TypeLoadMode.Static;

    /// <summary>Adds Wolverine to the host.</summary>
    /// <param name="builder">The host builder, needed for both its container and <c>UseWolverine</c>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// <c>ConnectionStrings:DefaultConnection</c> is not set, so there is nowhere to store messages.
    /// </exception>
    public static WebApplicationBuilder AddHostpanelWolverine(this WebApplicationBuilder builder)
    {
        // Read from the builder's configuration because Wolverine is configured before the container
        // exists. A missing value refuses startup by name rather than running without durability.
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not set; Wolverine's durable message storage needs it.");
        }

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

            // Static mode has no fallback, and before Wolverine 6.30 a handler whose generated type was
            // missing (a stale or skipped `codegen write`) booted a healthy host that lost every message of
            // that type. From 6.30 the host itself refuses to start in Static mode when a handler chain's
            // generated type is not in the assembly (MissingPreBuiltTypesException, naming the chains).
            // This flag adds JasperFx's environment check for the same thing over every code collection,
            // handlers or not (`check-env`, and anything added later), and is on exactly where handler code
            // is loaded rather than compiled. Production here means every environment but Development, the
            // same split TypeLoadModeFor makes.
            opts.Services.CritterStackDefaults(defaults =>
            {
                defaults.Production.AssertAllPreGeneratedTypesExist = AssertsPreGeneratedTypes(typeLoadMode);
            });

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

            // An event with several handlers gets one chain and one local queue per handler instead of one
            // chain running them all. Combined, a retry replays the whole chain, so a handler that failed
            // once re-ran its siblings that had already succeeded (a second mail, a second command); now
            // each handler is retried and dead-lettered on its own. A message with one handler keeps its
            // single chain, so InvokeAsync on a command or query is unchanged. An event with several
            // handlers can no longer be invoked, only published: its own chain has no handler left.
            opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

            // Envelopes, scheduled retries and dead letters live in the application's database, in
            // Wolverine's own schema. Wolverine creates and upgrades those tables itself at start (an
            // additive, idempotent migration) rather than through an EF migration: the shape is the
            // framework's and changes with its version, and an EF-owned copy would pull framework
            // tables into the model diff baseline and the PascalCase naming guard.
            opts.PersistMessagesWithPostgresql(connectionString, MessageStorageSchema);
            opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;

            // Registers IDbContextOutbox, which DomainEventDispatchInterceptor uses to write a save's
            // events in the save's own transaction. AutoApplyTransactions is deliberately not turned
            // on: it would wrap every handler that takes the context in a second transaction and an
            // extra SaveChangesAsync, changing the unit of work of handlers that already save
            // themselves (several on purpose more than once, per item), and it would not cover saves
            // made outside a handler (startup and hosted services, seeders), which the interceptor does.
            opts.UseEntityFrameworkCoreTransactions();

            // A local queue that carries published messages nobody waits on (events, commands sent for
            // later) keeps them in the database inbox, so a message accepted before a crash or a deploy is
            // still delivered afterwards. The queues of request/response use cases, which a controller
            // invokes inline and which never carry a message, stay in memory: a durable receiver for each
            // of them cost hundreds of idle inboxes and timers per host. See DurableQueuePolicy.
            opts.Policies.ConfigureConventionalLocalRouting().Named(LocalQueueNames.For);
            opts.Policies.Add<DurableQueuePolicy>();

            // The exception: recurring jobs (ResetDemoDataCommand is one) run on one in-memory queue.
            // Each pass schedules the next itself, and RecurringJobs.StartAsync and the demo startup
            // re-create the chain on every start from JobRuns. A durably scheduled pass would survive
            // the restart and be scheduled again, so every restart would add a chain. The queue's
            // explicit BufferedInMemory is applied after the durable-queues policy, so it wins, and a
            // scheduled message on a buffered queue stays in memory while
            // AlwaysMakeScheduledMessagesDurable keeps its default, false; do not turn it on.
            opts.Publish(publish =>
            {
                publish.MessagesImplementing<IRecurringJob>();
                publish.ToLocalQueue(RecurringJobsQueue).BufferedInMemory();
            });

            // Retries and dead letters for queued, fire-and-forget chains only; request/response chains
            // and recurring jobs are excluded, see the policy for why.
            opts.Policies.Add<DurableDeliveryRetryPolicy>();

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
