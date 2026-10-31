using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeds for the tests of members an identity plugin vouches for: links a client to an external account and
/// signs in a member the stub plugin maps to it.
/// </summary>
public static class PluginMemberSeeds
{
    /// <summary>The system name the stub plugin links its accounts under.</summary>
    public const string Provider = "crm";

    /// <summary>Links an existing client to a fresh external account.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client to link.</param>
    /// <returns>The account id in the plugin system.</returns>
    public static async Task<string> LinkAccountAsync(IntegrationTestFactory factory, int clientId)
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.SetExternalAccount(Provider, accountId);
        await db.SaveChangesAsync(CancellationToken.None);

        return accountId;
    }

    /// <summary>Registers a user and makes the stub plugin map them to <paramref name="accountId"/>.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="accountId">The account id in the plugin system the member belongs to.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <param name="canManagePaymentMethods">Whether the plugin lets the member manage payment methods.</param>
    /// <returns>The member, to dispose when the test ends.</returns>
    public static async Task<PluginMember> SignInMemberAsync(
        IntegrationTestFactory factory, string label, string accountId, bool canPurchase = false, bool canManagePaymentMethods = false)
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            registered.UserId,
            new ClientAccess(new ExternalAccount(Provider, accountId, null, null), canPurchase, canManagePaymentMethods));
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = registered.Client.DefaultRequestHeaders.Authorization;

        return new PluginMember(host, http, registered.ClientId, registered.Email);
    }

    /// <summary>
    /// Creates a user who holds no stored client role and owns no client record, and makes the stub plugin map
    /// them to <paramref name="accountId"/>: a member who only exists through the plugin.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">Distinguishes the created address between tests.</param>
    /// <param name="accountId">The account id in the plugin system the member belongs to.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <returns>The member, to dispose when the test ends. <see cref="PluginMember.OwnClientId"/> is 0.</returns>
    public static async Task<PluginMember> SignInRolelessMemberAsync(
        IntegrationTestFactory factory, string label, string accountId, bool canPurchase = false)
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            user.UserId,
            new ClientAccess(new ExternalAccount(Provider, accountId, null, null), canPurchase, CanManagePaymentMethods: false));

        return WithPlugin(factory, plugin, user.Client, user.Email);
    }

    /// <summary>Creates a user with no stored client role and no client record, served by a host with the given plugin.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">Distinguishes the created address between tests.</param>
    /// <param name="plugin">The enabled plugin, or <see langword="null"/> for none.</param>
    /// <returns>The user, to dispose when the test ends. <see cref="PluginMember.OwnClientId"/> is 0.</returns>
    public static async Task<PluginMember> SignInRolelessUserAsync(
        IntegrationTestFactory factory, string label, IClientResolver? plugin)
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, label);

        return WithPlugin(factory, plugin, user.Client, user.Email);
    }

    /// <summary>
    /// Creates a user with no stored client role and no client record, served by a host whose identity plugin
    /// resolution is <paramref name="resolver"/>: for a plugin that cannot be resolved at all.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">Distinguishes the created address between tests.</param>
    /// <param name="resolver">The port that hands the host its identity plugin.</param>
    /// <returns>The user and their subject, to dispose when the test ends.</returns>
    public static async Task<(PluginMember Member, string UserId)> SignInRolelessUserThroughAsync(
        IntegrationTestFactory factory, string label, IIdentityPluginResolver resolver)
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, label);

        return (WithResolver(factory, resolver, user.Client, user.Email), user.UserId);
    }

    /// <summary>
    /// Registers a user who holds the stored client role and owns a client record, served by a host whose
    /// identity plugin resolution is <paramref name="resolver"/>.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="resolver">The port that hands the host its identity plugin.</param>
    /// <returns>The user, to dispose when the test ends.</returns>
    public static async Task<PluginMember> SignInClientThroughAsync(
        IntegrationTestFactory factory, string label, IIdentityPluginResolver resolver)
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(factory, label);

        return WithResolver(factory, resolver, registered.Client, registered.Email) with { OwnClientId = registered.ClientId };
    }

    /// <summary>Builds a host whose identity plugin is <paramref name="plugin"/> and a client carrying the bearer of <paramref name="signedIn"/>.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="plugin">The enabled plugin, or <see langword="null"/> for none.</param>
    /// <param name="signedIn">The client whose bearer token is reused.</param>
    /// <param name="email">The user's address.</param>
    /// <returns>The host, its client and the address.</returns>
    private static PluginMember WithPlugin(IntegrationTestFactory factory, IClientResolver? plugin, HttpClient signedIn, string email) =>
        WithResolver(factory, new FakeIdentityPluginResolver(plugin), signedIn, email);

    /// <summary>Builds a host whose identity plugin resolution is <paramref name="resolver"/> and a client carrying the bearer of <paramref name="signedIn"/>.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="resolver">The port that hands the host its identity plugin.</param>
    /// <param name="signedIn">The client whose bearer token is reused.</param>
    /// <param name="email">The user's address.</param>
    /// <returns>The host, its client and the address.</returns>
    private static PluginMember WithResolver(IntegrationTestFactory factory, IIdentityPluginResolver resolver, HttpClient signedIn, string email)
    {
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(resolver)));
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;

        return new PluginMember(host, http, 0, email);
    }
}
