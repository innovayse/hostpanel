namespace Innovayse.Application.Tests.Services;

using Innovayse.Application.Common;
using Innovayse.Application.Services.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Proves the client-service ownership rule is scoped to the caller.
/// <para>
/// This rule is new. Before it, no client-facing service use case checked anything at all:
/// <c>MyServicesController</c> took a service id off the route and handed it to
/// <c>GetCPanelSsoUrlQuery</c>, <c>CancelServiceCommand</c>, <c>SetupServiceCommand</c>,
/// <c>ChangePasswordCommand</c> and <c>GetCancellationStatusQuery</c> unexamined. All five now
/// go through the client-facing <c>My*</c> handlers covered by
/// <c>CallerScopedServiceMessageTests</c>; this file tests the rule itself, to the same standard
/// as the domain, ticket and invoice rules it is modelled on.
/// </para>
/// </summary>
public sealed class ServiceOwnershipTests
{
    /// <summary>Identity subject of the caller in every test below.</summary>
    private const string CallerSubject = "user-caller";

    /// <summary>A client id that is deliberately not the caller's, so ownership must not match.</summary>
    private const int StrangersClientId = 4242;

    /// <summary>The service id every probe asks for.</summary>
    private const int ServiceId = 7;

    /// <summary>Builds the caller's own client record. Its <c>Id</c> is 0, the unsaved default.</summary>
    /// <returns>A client owned by <see cref="CallerSubject"/>.</returns>
    private static Client CallerClient() => Client.Create(CallerSubject, "Jane", "Doe", "jane@example.com");

    /// <summary>Builds a service belonging to the given client.</summary>
    /// <param name="clientId">Owning client id.</param>
    /// <returns>A pending service on that account.</returns>
    private static ClientService ServiceOf(int clientId) =>
        ClientService.Create(clientId, productId: 1, billingCycle: "monthly");

    /// <summary>
    /// Builds an <see cref="IServiceOwnership"/> over the given world.
    /// </summary>
    /// <param name="client">What the client repository answers for the caller's subject.</param>
    /// <param name="service">What the service repository answers for <see cref="ServiceId"/>.</param>
    /// <returns>The rule under test.</returns>
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

    /// <summary>The whole point: a service belonging to another client is not readable.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenServiceBelongsToAnotherClient_RefusesAsync()
    {
        var ownership = OwnershipOver(CallerClient(), ServiceOf(StrangersClientId));

        await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => ownership.RequireOwnedByCallerAsync(ServiceId, CancellationToken.None));
    }

    /// <summary>
    /// A service that does not exist answers exactly as one that is somebody else's, and so does
    /// a caller with no client record. All three must be indistinguishable, or the sequential ids
    /// can be walked to find out which are real.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_MissingAndStrangersAndNoProfile_AnswerAlikeAsync()
    {
        var strangers = await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => OwnershipOver(CallerClient(), ServiceOf(StrangersClientId))
                .RequireOwnedByCallerAsync(ServiceId, CancellationToken.None));

        var missing = await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => OwnershipOver(CallerClient(), service: null)
                .RequireOwnedByCallerAsync(ServiceId, CancellationToken.None));

        var noProfile = await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => OwnershipOver(client: null, ServiceOf(StrangersClientId))
                .RequireOwnedByCallerAsync(ServiceId, CancellationToken.None));

        Assert.Equal(MyServiceNotFoundException.PublicMessage, strangers.Message);
        Assert.Equal(strangers.Message, missing.Message);
        Assert.Equal(strangers.Message, noProfile.Message);
    }

    /// <summary>The refusal must not echo the probed id back to the caller.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenRefusing_DoesNotNameTheServiceInTheMessageAsync()
    {
        var refusal = await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => OwnershipOver(CallerClient(), ServiceOf(StrangersClientId))
                .RequireOwnedByCallerAsync(ServiceId, CancellationToken.None));

        Assert.DoesNotContain(ServiceId.ToString(), refusal.Message, StringComparison.Ordinal);
        Assert.Equal(ServiceId, refusal.ServiceId);
    }

    /// <summary>The rule must not refuse the caller their own service.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenServiceIsTheCallersOwn_AllowsAsync()
    {
        var ownership = OwnershipOver(CallerClient(), ServiceOf(clientId: 0));

        await ownership.RequireOwnedByCallerAsync(ServiceId, CancellationToken.None);
    }

    /// <summary>
    /// The client id a handler goes on to filter by is the caller's own, read from the
    /// credential -- never anything a caller could have named.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireCallerClientIdAsync_ReturnsTheCallersOwnClientIdAsync()
    {
        var ownership = OwnershipOver(CallerClient(), ServiceOf(clientId: 0));

        var clientId = await ownership.RequireCallerClientIdAsync(CancellationToken.None);

        Assert.Equal(0, clientId);
        Assert.NotEqual(StrangersClientId, clientId);
    }
}
