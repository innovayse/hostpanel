namespace Innovayse.Application.Tests.Services;

using System.Reflection;
using Innovayse.Application.Billing.Queries.GetMyServiceInvoices;
using Innovayse.Application.Common;
using Innovayse.Application.Provisioning.Commands.ChangeMyServicePassword;
using Innovayse.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;
using Innovayse.Application.Provisioning.Queries.GetMyServiceCPanelSsoUrl;
using Innovayse.Application.Services.Commands.CancelMyService;
using Innovayse.Application.Services.Commands.CancelService;
using Innovayse.Application.Services.Commands.SetupMyService;
using Innovayse.Application.Services.Commands.SetupService;
using Innovayse.Application.Services.Common;
using Innovayse.Application.Services.Queries.GetCancellationStatus;
using Innovayse.Application.Services.Queries.GetMyServiceCancellationStatus;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Moq;
using Wolverine;
using Xunit;

/// <summary>
/// Proves that every client-facing service route refuses a service the caller does not own,
/// before any work is done, and that the refusal is the same one a service that does not exist
/// produces.
/// <para>
/// This is the regression suite for a live defect: <c>MyServicesController</c> took a service id
/// straight off the route on five actions and checked nothing, so any authenticated customer
/// could walk sequential ids and obtain a control-panel single-sign-on URL into another
/// customer's hosting account, cancel their service, or change their hosting password.
/// </para>
/// <para>
/// The last two tests in this file are the ones that matter beyond today. The defect was not
/// five mistakes; it was that nothing failed when a route was written without a check. The
/// reflection tests here — and <c>MyServicesRoutesAreOwnershipScopedTests</c> in
/// <c>Innovayse.Integration.Tests</c>, which reaches the controller this project cannot see —
/// fail on the <b>next</b> such route rather than on these five.
/// </para>
/// </summary>
public sealed class CallerScopedServiceMessageTests
{
    /// <summary>Identity subject of the caller in every test below.</summary>
    private const string CallerSubject = "user-caller";

    /// <summary>The service id every probe asks for.</summary>
    private const int ServiceId = 7;

    /// <summary>A client id that is deliberately not the caller's.</summary>
    private const int StrangersClientId = 4242;

    /// <summary>
    /// The five routes this defect concerned, named as their URL segment so a failure reads as
    /// the endpoint that is broken rather than as a type name.
    /// </summary>
    /// <returns>The route keys <see cref="DispatchAsync"/> understands.</returns>
    public static TheoryData<string> ClientFacingRoutes() =>
        new("cpanel-sso", "cancel", "setup", "cancellation-status", "change-password");

    /// <summary>
    /// Runs the client-facing use case behind one route, over the given ownership rule and bus.
    /// </summary>
    /// <remarks>
    /// The handlers are constructed directly rather than resolved, so this asserts about the code
    /// that runs in production and not about a container registration. Their return types differ
    /// (a URL, a status, nothing); all are awaited as <see cref="Task"/> because no test here
    /// asserts on the value — the question is only whether the work happened at all.
    /// </remarks>
    /// <param name="route">One of the keys from <see cref="ClientFacingRoutes"/>.</param>
    /// <param name="ownership">The ownership rule to run under.</param>
    /// <param name="bus">The bus the shared use case would be dispatched through.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the use case does.</returns>
    private static Task DispatchAsync(
        string route, IServiceOwnership ownership, IMessageBus bus, CancellationToken ct) => route switch
        {
            "cpanel-sso" => new GetMyServiceCPanelSsoUrlHandler(ownership, bus)
                .HandleAsync(new GetMyServiceCPanelSsoUrlQuery(ServiceId), ct),
            "cancel" => new CancelMyServiceHandler(ownership, bus)
                .HandleAsync(new CancelMyServiceCommand(ServiceId, "Immediate", null), ct),
            "setup" => new SetupMyServiceHandler(ownership, bus)
                .HandleAsync(new SetupMyServiceCommand(ServiceId, "example.com", "jdoe", "pw"), ct),
            "cancellation-status" => new GetMyServiceCancellationStatusHandler(ownership, bus)
                .HandleAsync(new GetMyServiceCancellationStatusQuery(ServiceId), ct),
            "change-password" => new ChangeMyServicePasswordHandler(ownership, bus)
                .HandleAsync(new ChangeMyServicePasswordCommand(ServiceId, "pw"), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown route."),
        };

    /// <summary>Builds the caller's own client record. Its <c>Id</c> is 0, the unsaved default.</summary>
    /// <returns>A client owned by <see cref="CallerSubject"/>.</returns>
    private static Client CallerClient() => Client.Create(CallerSubject, "Jane", "Doe", "jane@example.com");

    /// <summary>Builds a service belonging to the given client.</summary>
    /// <param name="clientId">Owning client id.</param>
    /// <returns>A pending service on that account.</returns>
    private static ClientService ServiceOf(int clientId) =>
        ClientService.Create(clientId, productId: 1, billingCycle: "monthly");

    /// <summary>
    /// Builds the real <see cref="ServiceOwnership"/> over a world in which the caller's client
    /// record and the stored service are as given.
    /// </summary>
    /// <remarks>
    /// The real rule is used rather than a mock of it: a mocked rule would prove the handlers
    /// call something, not that a stranger is actually refused.
    /// </remarks>
    /// <param name="client">What the client repository answers for the caller's subject.</param>
    /// <param name="service">What the service repository answers for <see cref="ServiceId"/>.</param>
    /// <returns>The ownership rule the handlers run under.</returns>
    private static IServiceOwnership OwnershipOver(Client? client, ClientService? service)
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByUserIdAsync(CallerSubject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var services = new Mock<IClientServiceRepository>();
        services.Setup(r => r.FindByIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        var caller = new Mock<ICurrentRequestContext>();
        caller.Setup(c => c.RequireUserId()).Returns(CallerSubject);

        return new ServiceOwnership(services.Object, clients.Object, caller.Object);
    }

    /// <summary>
    /// The caller's own service still works. A guard that refused everybody would pass every
    /// other test in this file.
    /// </summary>
    /// <param name="route">The route under test.</param>
    /// <returns>A task representing the test.</returns>
    [Theory]
    [MemberData(nameof(ClientFacingRoutes))]
    public async Task Route_WhenServiceIsTheCallersOwn_ReachesTheSharedUseCaseAsync(string route)
    {
        var bus = new Mock<IMessageBus>();

        // The caller's client record is unsaved, so its id is 0; the service is created against
        // the same 0, which is what makes it theirs.
        await DispatchAsync(route, OwnershipOver(CallerClient(), ServiceOf(clientId: 0)), bus.Object, CancellationToken.None);

        Assert.Single(bus.Invocations);
    }

    /// <summary>
    /// The defect itself: a service belonging to another client is refused, and nothing is
    /// dispatched — so no provider is called, no password is set and no cancellation is recorded
    /// against somebody else's account.
    /// </summary>
    /// <param name="route">The route under test.</param>
    /// <returns>A task representing the test.</returns>
    [Theory]
    [MemberData(nameof(ClientFacingRoutes))]
    public async Task Route_WhenServiceBelongsToAnotherClient_RefusesBeforeDoingAnyWorkAsync(string route)
    {
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);

        await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => DispatchAsync(
                route, OwnershipOver(CallerClient(), ServiceOf(StrangersClientId)), bus.Object, CancellationToken.None));

        bus.VerifyNoOtherCalls();
    }

    /// <summary>
    /// A stranger's service and an id that exists nowhere must be indistinguishable — same
    /// exception type, same sentence, and the id never echoed back. Otherwise the route is an
    /// oracle for which of the sequential service ids are real.
    /// </summary>
    /// <param name="route">The route under test.</param>
    /// <returns>A task representing the test.</returns>
    [Theory]
    [MemberData(nameof(ClientFacingRoutes))]
    public async Task Route_RefusesAStrangersServiceExactlyAsItRefusesOneThatDoesNotExistAsync(string route)
    {
        var strangers = await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => DispatchAsync(
                route,
                OwnershipOver(CallerClient(), ServiceOf(StrangersClientId)),
                new Mock<IMessageBus>(MockBehavior.Strict).Object,
                CancellationToken.None));

        var missing = await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => DispatchAsync(
                route,
                OwnershipOver(CallerClient(), service: null),
                new Mock<IMessageBus>(MockBehavior.Strict).Object,
                CancellationToken.None));

        Assert.Equal(MyServiceNotFoundException.PublicMessage, strangers.Message);
        Assert.Equal(strangers.Message, missing.Message);
        Assert.Equal(strangers.GetType(), missing.GetType());
        Assert.DoesNotContain(ServiceId.ToString(), strangers.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guard that outlives this fix: any message declaring itself caller-scoped must have a
    /// handler that takes <see cref="IServiceOwnership"/>. A new client-facing service use case
    /// written without the check fails here rather than in production.
    /// </summary>
    [Fact]
    public void EveryCallerScopedMessage_HasAHandlerThatTakesTheOwnershipRule()
    {
        var assembly = typeof(IServiceOwnership).Assembly;

        var marked = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => typeof(ICallerScopedServiceMessage).IsAssignableFrom(t))
            .ToList();

        // If this ever reads zero the test has stopped testing anything -- a renamed marker or a
        // moved assembly would otherwise leave it silently green.
        Assert.NotEmpty(marked);

        var unguarded = marked
            .Where(message => !HandlersOf(assembly, message).Any(HandlerTakesOwnership))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unguarded);

        // Every marked message must actually be handled, or the check above passes vacuously.
        var unhandled = marked.Where(m => HandlersOf(assembly, m).Count == 0).Select(t => t.Name).ToList();
        Assert.Empty(unhandled);
    }

    /// <summary>
    /// The shared use cases an admin route also dispatches must <b>not</b> carry the marker.
    /// Marking them would make the ownership rule apply to staff acting on any client's service,
    /// which is legitimate — the split into <c>My*</c> messages exists precisely so the client
    /// path can be constrained without constraining the admin path.
    /// </summary>
    [Fact]
    public void SharedUseCasesTheAdminRoutesDispatch_AreNotCallerScoped()
    {
        Assert.False(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetCPanelSsoUrlQuery)));
        Assert.False(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(ChangePasswordCommand)));
        Assert.False(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(CancelServiceCommand)));
        Assert.False(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(SetupServiceCommand)));
        Assert.False(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetCancellationStatusQuery)));
    }

    /// <summary>
    /// The client-facing messages carry the marker. Named one by one rather than counted, so the
    /// list cannot be satisfied by some other type happening to implement it.
    /// </summary>
    [Fact]
    public void TheClientFacingServiceMessages_AreCallerScoped()
    {
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceCPanelSsoUrlQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(ChangeMyServicePasswordCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(CancelMyServiceCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(SetupMyServiceCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceCancellationStatusQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceInvoicesQuery)));
    }

    /// <summary>
    /// Finds the Wolverine handlers for a message: any type with a <c>HandleAsync</c> whose first
    /// parameter is that message. Wolverine discovers handlers by exactly this shape, so this
    /// looks for what it looks for rather than for a naming convention.
    /// </summary>
    /// <param name="assembly">The Application assembly.</param>
    /// <param name="message">The message type.</param>
    /// <returns>The handler types, which may be empty.</returns>
    private static IReadOnlyList<Type> HandlersOf(Assembly assembly, Type message) =>
        assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Any(m => m.Name == "HandleAsync"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == message))
            .ToList();

    /// <summary>
    /// Whether a handler is constructed with the ownership rule. Taking it as a dependency is the
    /// only way it can be called, and the handlers are small enough that a dependency taken and
    /// then ignored would not survive review — the behaviour tests above cover the five that
    /// exist today, and this covers the ones that do not exist yet.
    /// </summary>
    /// <param name="handler">The handler type.</param>
    /// <returns><see langword="true"/> when some constructor takes an <see cref="IServiceOwnership"/>.</returns>
    private static bool HandlerTakesOwnership(Type handler) =>
        handler.GetConstructors()
            .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IServiceOwnership)));
}
