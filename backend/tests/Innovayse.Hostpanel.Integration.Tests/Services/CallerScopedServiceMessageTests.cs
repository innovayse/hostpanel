using System.Reflection;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyServiceInvoices;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangeMyServicePassword;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetControlPanelSsoUrl;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServiceControlPanelSsoUrl;
using Innovayse.Hostpanel.Application.Services.Commands.AddMySshKey;
using Innovayse.Hostpanel.Application.Services.Commands.CancelMyService;
using Innovayse.Hostpanel.Application.Services.Commands.CancelService;
using Innovayse.Hostpanel.Application.Services.Commands.RevokeMySshKey;
using Innovayse.Hostpanel.Application.Services.Commands.SetupMyService;
using Innovayse.Hostpanel.Application.Services.Commands.SetupService;
using Innovayse.Hostpanel.Application.Services.Queries.GetCancellationStatus;
using Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceCancellationStatus;
using Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceHostingInfo;
using Innovayse.Hostpanel.Application.Services.Queries.GetMySshAccess;
using Innovayse.Hostpanel.Application.Services.Queries.ListMySshKeys;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Proves that every client-facing service message is marked, and every marked message has a
/// handler wired to enforce it, without exercising a handler's runtime behaviour: that half, a
/// stranger's service actually being refused over HTTP, is
/// <see cref="MyServicesRoutesAreOwnershipScopedTests"/> in this project, which reaches the real database and
/// the real ownership rule together.
/// <para>
/// A client-facing route that takes a service id must check that the caller owns that service;
/// otherwise one customer could act on another customer's hosting account.
/// </para>
/// <para>
/// Checking each existing route does not stop the next one being written without a check. These
/// reflection tests fail on the <b>next</b> such message, and
/// <see cref="MyServicesRoutesAreOwnershipScopedTests"/> asserts over HTTP that each route refuses a
/// stranger's service.
/// </para>
/// </summary>
/// <remarks>Pure reflection over the Application assembly, no mock of our own
/// code and no database.</remarks>
public sealed class CallerScopedServiceMessageTests
{
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

        // If this ever reads zero the test has stopped testing anything: a renamed marker or a
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
    /// which is legitimate; the split into <c>My*</c> messages exists precisely so the client
    /// path can be constrained without constraining the admin path.
    /// </summary>
    [Fact]
    public void SharedUseCasesTheAdminRoutesDispatch_AreNotCallerScoped()
    {
        Assert.False(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetControlPanelSsoUrlQuery)));
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
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceControlPanelSsoUrlQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(ChangeMyServicePasswordCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(CancelMyServiceCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(SetupMyServiceCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceCancellationStatusQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceHostingInfoQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMyServiceInvoicesQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(GetMySshAccessQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(ListMySshKeysQuery)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(AddMySshKeyCommand)));
        Assert.True(typeof(ICallerScopedServiceMessage).IsAssignableFrom(typeof(RevokeMySshKeyCommand)));
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
    /// then ignored would not survive review. The behaviour is proven end-to-end by
    /// <see cref="MyServicesRoutesAreOwnershipScopedTests"/>, and this covers the ones that do not exist yet.
    /// </summary>
    /// <param name="handler">The handler type.</param>
    /// <returns><see langword="true"/> when some constructor takes an <see cref="IServiceOwnership"/>.</returns>
    private static bool HandlerTakesOwnership(Type handler) =>
        handler.GetConstructors()
            .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IServiceOwnership)));
}
